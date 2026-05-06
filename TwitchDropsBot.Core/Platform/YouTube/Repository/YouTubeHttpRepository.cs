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

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the watch URL of the active live stream for the given handle,
    /// or <c>null</c> when the channel is not currently live.
    /// </summary>
    /// <remarks>
    /// Calls InnerTube's <c>/browse</c> endpoint directly with the channel handle
    /// (<c>@name</c>). The response contains the channel's home page content;
    /// a depth-first search over all <c>videoRenderer</c> objects is performed to
    /// find the first one carrying a LIVE badge.
    /// </remarks>
    public async Task<string?> GetActiveLiveStreamAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        var browseId = NormalizeHandle(handle);
        _logger.LogTrace("Browsing channel '{BrowseId}' via InnerTube to detect live stream", browseId);

        using var doc = await _innerTube.BrowseAsync(browseId, null, cancellationToken);

        var video = FindLiveVideoRenderer(doc.RootElement);
        if (video is null)
        {
            _logger.LogTrace("No active live stream found for handle '{Handle}'", handle);
            return null;
        }

        var url = $"https://www.youtube.com/watch?v={video}";
        _logger.LogTrace("Active live stream found: {Url}", url);
        return url;
    }

    /// <summary>
    /// Returns <c>true</c> when the video with the given ID is currently an active live stream.
    /// </summary>
    /// <remarks>
    /// Uses the InnerTube <c>/player</c> endpoint.
    /// <c>videoDetails.isLive</c> is only present and <c>true</c> while a stream is ongoing.
    /// </remarks>
    public async Task<bool> IsVideoLiveAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking if video {VideoId} is still live", videoId);

        using var doc = await _innerTube.PlayerAsync(videoId, cancellationToken);

        if (!doc.RootElement.TryGetProperty("videoDetails", out var details))
            return false;

        // Primary signal: isLive is only present while the broadcast is active.
        if (details.TryGetProperty("isLive", out var isLive) &&
            isLive.ValueKind == JsonValueKind.True)
            return true;

        // Secondary signal: isLiveContent=true + lengthSeconds="0" also indicates an active stream.
        if (details.TryGetProperty("isLiveContent", out var isLiveContent) &&
            isLiveContent.ValueKind == JsonValueKind.True &&
            details.TryGetProperty("lengthSeconds", out var len) &&
            len.GetString() == "0")
            return true;

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the handle is prefixed with <c>@</c> as expected by InnerTube's
    /// <c>/browse</c> endpoint. UC… channel IDs are passed through unchanged.
    /// </summary>
    private static string NormalizeHandle(string handle)
    {
        if (handle.StartsWith("UC", StringComparison.Ordinal) && handle.Length > 10)
            return handle;

        return handle.StartsWith("@", StringComparison.Ordinal) ? handle : "@" + handle;
    }

    /// <summary>
    /// Performs a depth-first search over the InnerTube JSON response and returns
    /// the video ID of the first <c>videoRenderer</c> object that carries a LIVE badge,
    /// or <c>null</c> when no live video is found.
    /// </summary>
    private static string? FindLiveVideoRenderer(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("videoId", out var videoIdProp) &&
                videoIdProp.ValueKind == JsonValueKind.String &&
                IsRendererLive(element))
            {
                return videoIdProp.GetString();
            }

            foreach (var prop in element.EnumerateObject())
            {
                var result = FindLiveVideoRenderer(prop.Value);
                if (result is not null)
                    return result;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var result = FindLiveVideoRenderer(item);
                if (result is not null)
                    return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when the video renderer object carries any recognised
    /// live indicator: a time-status overlay with style <c>LIVE</c>, or a
    /// <c>BADGE_STYLE_TYPE_LIVE_NOW</c> metadata badge.
    /// </summary>
    private static bool IsRendererLive(JsonElement renderer)
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
                    mbr.TryGetProperty("style", out var badgeStyle) &&
                    badgeStyle.GetString() == "BADGE_STYLE_TYPE_LIVE_NOW")
                    return true;
            }
        }

        return false;
    }
}
