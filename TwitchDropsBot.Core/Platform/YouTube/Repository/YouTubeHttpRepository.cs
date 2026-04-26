using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.Shared.Repository;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

public class YouTubeHttpRepository : BotRepository<YouTubeUser>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    private const string FakeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36";

    private const string InnerTubeBrowseUrl = "https://www.youtube.com/youtubei/v1/browse";

    /// <summary>
    /// InnerTube protobuf-encoded parameter that filters the channel browse result
    /// to the "Live" tab only.
    /// </summary>
    private const string LiveTabParams = "EgJIAQ==";

    public YouTubeHttpRepository(YouTubeUser user, ILogger logger)
    {
        BotUser = user;
        _logger = logger;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(FakeUserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    /// <summary>
    /// Returns the watch URL of the active live stream on the given channel via the
    /// YouTube InnerTube API, or <c>null</c> when the channel is not currently live.
    /// </summary>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="JsonException">Thrown when the response cannot be parsed.</exception>
    public async Task<string?> GetActiveLiveStreamAsync(
        string channelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking for active live stream on channel {ChannelId} via InnerTube", channelId);

        // InnerTube accepts both "UC…" channel IDs and "@handle" values.
        var browseId = channelId.StartsWith("UC", StringComparison.Ordinal)
            ? channelId
            : $"@{channelId}";

        var requestBody = new
        {
            context = new
            {
                client = new
                {
                    clientName    = "WEB",
                    clientVersion = "2.20231121.08.00",
                    hl            = "en",
                    gl            = "US"
                }
            },
            browseId,
            @params = LiveTabParams
        };

        var json    = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(InnerTubeBrowseUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseBody);

        var videoId = FindFirstLiveVideoId(document.RootElement);
        if (videoId == null)
        {
            _logger.LogTrace("No active live stream found for channel {ChannelId}", channelId);
            return null;
        }

        var streamUrl = $"https://www.youtube.com/watch?v={videoId}";
        _logger.LogTrace("Found active live stream via InnerTube: {StreamUrl}", streamUrl);
        return streamUrl;
    }

    // -------------------------------------------------------------------------
    // JSON helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Recursively traverses the InnerTube JSON response and returns the
    /// <c>videoId</c> of the first <c>videoRenderer</c> that carries a LIVE badge,
    /// or <c>null</c> if none is found.
    /// </summary>
    private static string? FindFirstLiveVideoId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("videoRenderer", out var renderer))
            {
                var liveId = ExtractLiveVideoIdFromRenderer(renderer);
                if (liveId != null)
                    return liveId;
            }

            foreach (var property in element.EnumerateObject())
            {
                var result = FindFirstLiveVideoId(property.Value);
                if (result != null)
                    return result;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var result = FindFirstLiveVideoId(item);
                if (result != null)
                    return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the <c>videoId</c> from a <c>videoRenderer</c> object if it is
    /// currently live, otherwise returns <c>null</c>.
    /// </summary>
    private static string? ExtractLiveVideoIdFromRenderer(JsonElement renderer)
    {
        if (!renderer.TryGetProperty("videoId", out var videoIdEl))
            return null;

        var videoId = videoIdEl.GetString();
        if (string.IsNullOrEmpty(videoId))
            return null;

        // Check top-level badges (e.g. "BADGE_STYLE_TYPE_LIVE_NOW").
        if (renderer.TryGetProperty("badges", out var badges))
        {
            foreach (var badge in badges.EnumerateArray())
            {
                if (badge.TryGetProperty("metadataBadgeRenderer", out var metaBadge) &&
                    metaBadge.TryGetProperty("style", out var style) &&
                    style.GetString() == "BADGE_STYLE_TYPE_LIVE_NOW")
                {
                    return videoId;
                }
            }
        }

        // Check thumbnail overlay (e.g. style "LIVE").
        if (renderer.TryGetProperty("thumbnailOverlays", out var overlays))
        {
            foreach (var overlay in overlays.EnumerateArray())
            {
                if (overlay.TryGetProperty("thumbnailOverlayTimeStatusRenderer", out var timeStatus) &&
                    timeStatus.TryGetProperty("style", out var style) &&
                    style.GetString() == "LIVE")
                {
                    return videoId;
                }
            }
        }

        return null;
    }
}
