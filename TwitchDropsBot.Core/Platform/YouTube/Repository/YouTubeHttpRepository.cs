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

    /// <summary>
    /// Returns the watch URL of the active live stream for the given handle,
    /// or <c>null</c> when the channel is not currently live.
    /// </summary>
    /// <remarks>
    /// Delegates to the <c>yt-dlp</c> subprocess bridge.
    /// </remarks>
    public async Task<string?> GetActiveLiveStreamAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking channel '{Handle}' for active live stream via yt-dlp", handle);

        var cookiesFilePath = BotUser.CookieLogin ? BotUser.CookiesFilePath : null;
        var url = await YoutubeYtDlpBridge.GetActiveLiveStreamAsync(handle, cookiesFilePath, cancellationToken);
        if (url is null)
        {
            _logger.LogTrace("No active live stream found for handle '{Handle}'", handle);
            return null;
        }

        _logger.LogTrace("Active live stream found: {Url}", url);
        return url;
    }

    /// <summary>
    /// Returns <c>true</c> when the video with the given ID is currently an active live stream.
    /// </summary>
    /// <remarks>
    /// Delegates to the <c>yt-dlp</c> subprocess bridge.
    /// </remarks>
    public async Task<bool> IsVideoLiveAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking if video {VideoId} is still live via yt-dlp", videoId);
        var cookiesFilePath = BotUser.CookieLogin ? BotUser.CookiesFilePath : null;
        return await YoutubeYtDlpBridge.IsVideoLiveAsync(videoId, cookiesFilePath, cancellationToken);
    }
}
