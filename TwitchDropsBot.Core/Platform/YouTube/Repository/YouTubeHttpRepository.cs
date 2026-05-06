using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using TwitchDropsBot.Core.Platform.YouTube.Bot;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

public class YouTubeHttpRepository : BotRepository<YouTubeUser>
{
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
    /// Delegates to the bundled Node.js worker which uses the
    /// <c>youtubei.js</c> library to call InnerTube's <c>/browse</c> endpoint.
    /// </remarks>
    public async Task<string?> GetActiveLiveStreamAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking channel '{Handle}' for active live stream via youtubei.js", handle);

        var videoId = await YoutubeNodeBridge.GetLiveVideoIdAsync(handle, cancellationToken);
        if (videoId is null)
        {
            _logger.LogTrace("No active live stream found for handle '{Handle}'", handle);
            return null;
        }

        var url = $"https://www.youtube.com/watch?v={videoId}";
        _logger.LogTrace("Active live stream found: {Url}", url);
        return url;
    }

    /// <summary>
    /// Returns <c>true</c> when the video with the given ID is currently an active live stream.
    /// </summary>
    /// <remarks>
    /// Delegates to the bundled Node.js worker which uses the
    /// <c>youtubei.js</c> library to call InnerTube's <c>/player</c> endpoint.
    /// </remarks>
    public async Task<bool> IsVideoLiveAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking if video {VideoId} is still live via youtubei.js", videoId);
        return await YoutubeNodeBridge.IsVideoLiveAsync(videoId, cancellationToken);
    }
}
