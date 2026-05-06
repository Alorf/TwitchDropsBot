using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using TwitchDropsBot.Core.Platform.YouTube.Bot;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

public class YouTubeHttpRepository : BotRepository<YouTubeUser>
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/125.0.0.0 Safari/537.36";

    // Single shared HttpClient — follows redirects by default (up to 50).
    private static readonly HttpClient Http = CreateHttpClient();

    private readonly ILogger _logger;

    public YouTubeHttpRepository(YouTubeUser user, ILogger logger)
    {
        BotUser = user;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the watch URL of the active live stream for the given handle,
    /// or <c>null</c> when the channel is not currently live.
    /// </summary>
    /// <remarks>
    /// YouTube redirects <c>/@handle/live</c> to <c>/watch?v=VIDEO_ID</c> when
    /// the channel is live. A plain HTTP GET is all that is required — no API
    /// keys and no InnerTube POST requests.
    /// </remarks>
    public async Task<string?> GetActiveLiveStreamAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        var liveUrl = BuildLiveUrl(handle);
        _logger.LogTrace("Checking for active live stream: GET {Url}", liveUrl);

        var response = await Http.GetAsync(liveUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        // After following redirects, RequestMessage.RequestUri is the final URL.
        var finalUrl = response.RequestMessage?.RequestUri?.ToString();

        if (finalUrl?.Contains("watch?v=", StringComparison.Ordinal) == true)
        {
            _logger.LogTrace("Active live stream found: {Url}", finalUrl);
            return finalUrl;
        }

        _logger.LogTrace("Channel '{Handle}' is not live (final URL: {FinalUrl})", handle, finalUrl);
        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when the video with the given ID is currently an active live stream.
    /// </summary>
    /// <remarks>
    /// Fetches the public watch page and checks for the <c>"isLive":true</c> marker
    /// that YouTube embeds in its page-level JSON data.
    /// </remarks>
    public async Task<bool> IsVideoLiveAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking if video {VideoId} is still live", videoId);

        var watchUrl = $"https://www.youtube.com/watch?v={videoId}";
        var html     = await Http.GetStringAsync(watchUrl, cancellationToken);

        // YouTube embeds minified JSON in the page; check both spacing variants to be safe.
        var isLive = html.Contains("\"isLive\":true",   StringComparison.Ordinal) ||
                     html.Contains("\"isLive\": true",  StringComparison.Ordinal);

        _logger.LogTrace("Video {VideoId} isLive={IsLive}", videoId, isLive);
        return isLive;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the YouTube live-check URL for the given handle or channel ID.
    /// Accepts <c>@handle</c>, plain <c>handle</c>, or <c>UC…</c> channel IDs.
    /// </summary>
    private static string BuildLiveUrl(string handle)
    {
        // UC… channel IDs use the /channel/ path; handles use the @-prefixed path.
        if (handle.StartsWith("UC", StringComparison.Ordinal) && handle.Length > 10)
            return $"https://www.youtube.com/channel/{handle}/live";

        var normalized = handle.StartsWith("@", StringComparison.Ordinal) ? handle : "@" + handle;
        return $"https://www.youtube.com/{normalized}/live";
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",      UserAgent);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",          "text/html,*/*;q=0.8");
        return client;
    }
}
