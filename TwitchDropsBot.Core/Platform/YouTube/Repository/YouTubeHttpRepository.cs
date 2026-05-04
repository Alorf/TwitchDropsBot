using Microsoft.Extensions.Logging;
using System.Text.Json;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using TwitchDropsBot.Core.Platform.YouTube.Bot;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

public class YouTubeHttpRepository : BotRepository<YouTubeUser>
{
    private readonly InnerTubeClient _innerTube;
    private readonly ILogger         _logger;

    public YouTubeHttpRepository(YouTubeUser user, ILogger logger)
    {
        BotUser    = user;
        _logger    = logger;
        _innerTube = new InnerTubeClient();
    }

    /// <summary>
    /// Returns the watch URL of the active live stream on the given channel,
    /// or <c>null</c> when the channel is not currently live.
    /// </summary>
    /// <remarks>
    /// Uses YouTube's internal InnerTube API directly, inspired by LuanRT/YouTube.js.
    /// A live stream is identified by a <c>LIVE</c> badge on the first entry in the
    /// channel's uploads playlist (most-recent content appears first).
    /// </remarks>
    public async Task<string?> GetActiveLiveStreamAsync(
        string channelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking for active live stream on channel {ChannelId}", channelId);

        // Resolve handles (@name, @handle, or any non-UC… input) to a canonical UC… channel ID.
        string resolvedId;
        if (channelId.StartsWith("UC", StringComparison.Ordinal) && channelId.Length > 2)
        {
            resolvedId = channelId;
        }
        else
        {
            _logger.LogDebug("'{ChannelId}' is not a UC… ID, resolving via InnerTube", channelId);
            resolvedId = await ResolveChannelIdAsync(channelId, cancellationToken) ?? string.Empty;

            if (string.IsNullOrEmpty(resolvedId) ||
                !resolvedId.StartsWith("UC", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Could not resolve '{ChannelId}' to a valid UC… channel ID (got '{Resolved}'). " +
                    "Check that the handle or name is correct.",
                    channelId, resolvedId);
                return null;
            }
        }

        // The uploads playlist ID is the channel ID with the prefix "UC" replaced by "UU".
        var uploadsId = "UU" + resolvedId.Substring(2);

        using var doc  = await _innerTube.BrowseAsync(uploadsId, null, cancellationToken);
        var first      = GetFirstVideo(doc.RootElement);

        if (first is { IsLive: true })
        {
            var url = $"https://www.youtube.com/watch?v={first.Value.VideoId}";
            _logger.LogTrace("Found active live stream: {Url}", url);
            return url;
        }

        _logger.LogTrace("No active live stream found for channel {ChannelId}", channelId);
        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when the video with the given ID is currently an active live stream.
    /// </summary>
    /// <remarks>
    /// Uses the InnerTube <c>/player</c> endpoint. The response field
    /// <c>videoDetails.isLive</c> is only present (and <c>true</c>) while a stream is ongoing.
    /// </remarks>
    public async Task<bool> IsVideoLiveAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking if video {VideoId} is still live", videoId);

        using var doc = await _innerTube.PlayerAsync(videoId, cancellationToken);

        if (!doc.RootElement.TryGetProperty("videoDetails", out var videoDetails))
            return false;

        // `isLive` is present only while the broadcast is ongoing.
        if (videoDetails.TryGetProperty("isLive", out var isLive) &&
            isLive.ValueKind == JsonValueKind.True)
            return true;

        // Secondary signal: isLiveContent=true and lengthSeconds="0" also indicates an active stream.
        if (videoDetails.TryGetProperty("isLiveContent", out var isLiveContent) &&
            isLiveContent.ValueKind == JsonValueKind.True &&
            videoDetails.TryGetProperty("lengthSeconds", out var lengthSeconds) &&
            lengthSeconds.GetString() == "0")
            return true;

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a channel handle (e.g. <c>@OWL</c> or plain <c>OWL</c>) to its
    /// canonical UC… channel ID by browsing the channel page via InnerTube.
    /// </summary>
    private async Task<string?> ResolveChannelIdAsync(string handle, CancellationToken ct)
    {
        var browseId = handle.StartsWith("@", StringComparison.Ordinal) ? handle : "@" + handle;
        using var doc = await _innerTube.BrowseAsync(browseId, null, ct);

        // metadata.channelMetadataRenderer.externalId
        if (doc.RootElement.TryGetProperty("metadata", out var metadata) &&
            metadata.TryGetProperty("channelMetadataRenderer", out var channelMeta) &&
            channelMeta.TryGetProperty("externalId", out var externalId))
            return externalId.GetString();

        // header.*.channelId  (c4TabbedHeaderRenderer, pageHeaderRenderer, etc.)
        if (doc.RootElement.TryGetProperty("header", out var header) &&
            header.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in header.EnumerateObject())
            {
                if (prop.Value.TryGetProperty("channelId", out var chId))
                    return chId.GetString();
            }
        }

        return null;
    }

    /// <summary>
    /// Depth-first search over the InnerTube JSON response that returns the
    /// first video renderer found and whether it carries a LIVE badge.
    /// </summary>
    private static (string VideoId, bool IsLive)? GetFirstVideo(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            // Any renderer object that has a `videoId` string property is a video entry.
            if (element.TryGetProperty("videoId", out var videoIdProp) &&
                videoIdProp.ValueKind == JsonValueKind.String)
                return (videoIdProp.GetString()!, IsVideoRendererLive(element));

            foreach (var property in element.EnumerateObject())
            {
                var result = GetFirstVideo(property.Value);
                if (result.HasValue)
                    return result;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var result = GetFirstVideo(item);
                if (result.HasValue)
                    return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when the video renderer JSON object carries any recognised
    /// live indicator (time-status overlay with style <c>LIVE</c>, or a
    /// <c>BADGE_STYLE_TYPE_LIVE_NOW</c> metadata badge).
    /// </summary>
    private static bool IsVideoRendererLive(JsonElement renderer)
    {
        // thumbnailOverlays: [{ thumbnailOverlayTimeStatusRenderer: { style: "LIVE" } }]
        if (renderer.TryGetProperty("thumbnailOverlays", out var overlays))
        {
            foreach (var overlay in overlays.EnumerateArray())
            {
                if (overlay.TryGetProperty("thumbnailOverlayTimeStatusRenderer", out var tsr) &&
                    tsr.TryGetProperty("style", out var style) &&
                    style.GetString() == "LIVE")
                    return true;
            }
        }

        // badges: [{ metadataBadgeRenderer: { style: "BADGE_STYLE_TYPE_LIVE_NOW" } }]
        if (renderer.TryGetProperty("badges", out var badges))
        {
            foreach (var badge in badges.EnumerateArray())
            {
                if (badge.TryGetProperty("metadataBadgeRenderer", out var mbr) &&
                    mbr.TryGetProperty("style", out var style) &&
                    style.GetString() == "BADGE_STYLE_TYPE_LIVE_NOW")
                    return true;
            }
        }

        return false;
    }
}
