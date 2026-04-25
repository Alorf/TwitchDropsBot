using System.Text.RegularExpressions;
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

    private static readonly Regex LiveVideoIdRegex =
        new Regex(@"vi/(.*?)/hqdefault_live\.jpg", RegexOptions.Compiled);

    public YouTubeHttpRepository(YouTubeUser user, ILogger logger)
    {
        BotUser = user;
        _logger = logger;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(FakeUserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    /// <summary>
    /// Returns the watch URL of the active live stream on the given channel, or
    /// <c>null</c> when the channel is not currently live.
    /// </summary>
    public async Task<string?> GetActiveLiveStreamAsync(
        string channelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking for active live stream on channel {ChannelId}", channelId);

        try
        {
            var url = $"https://www.youtube.com/@{channelId}";
            var html = await _httpClient.GetStringAsync(url, cancellationToken);

            if (!html.Contains("hqdefault_live.jpg"))
            {
                _logger.LogTrace("No active live stream found for channel {ChannelId}", channelId);
                return null;
            }

            var match = LiveVideoIdRegex.Match(html);
            if (!match.Success)
            {
                _logger.LogTrace("Live indicator found but could not extract video ID for channel {ChannelId}", channelId);
                return null;
            }

            var videoId = match.Groups[1].Value;
            var streamUrl = $"https://www.youtube.com/watch?v={videoId}";
            _logger.LogTrace("Found active live stream: {StreamUrl}", streamUrl);
            return streamUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check live stream status for channel {ChannelId}", channelId);
            return null;
        }
    }
}
