using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using YoutubeExplode;
using YoutubeExplode.Channels;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

public class YouTubeHttpRepository : BotRepository<YouTubeUser>
{
    private readonly YoutubeClient _youtube;
    private readonly ILogger _logger;

    public YouTubeHttpRepository(YouTubeUser user, ILogger logger)
    {
        BotUser = user;
        _logger = logger;
        _youtube = new YoutubeClient();
    }

    /// <summary>
    /// Returns the watch URL of the active live stream on the given channel,
    /// or <c>null</c> when the channel is not currently live.
    /// </summary>
    /// <remarks>
    /// Uses YoutubeExplode to fetch the channel's latest uploads.
    /// An actively live stream is always listed first in the uploads playlist and has a
    /// <c>null</c> duration (as documented by <see cref="YoutubeExplode.Videos.IVideo.Duration"/>).
    /// </remarks>
    public async Task<string?> GetActiveLiveStreamAsync(
        string channelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking for active live stream on channel {ChannelId}", channelId);

        // Resolve to a ChannelId. Inputs can be either a "UC…" ID or a plain handle.
        ChannelId resolvedId;
        if (ChannelId.TryParse(channelId) is { } directId)
        {
            resolvedId = directId;
        }
        else
        {
            var channel = await _youtube.Channels.GetByHandleAsync(
                ChannelHandle.Parse(channelId), cancellationToken);
            resolvedId = channel.Id;
        }

        // The uploads playlist lists the most recent content first.
        // An actively live stream appears at the top with Duration == null.
        await foreach (var video in _youtube.Channels.GetUploadsAsync(resolvedId, cancellationToken))
        {
            if (video.Duration is null)
            {
                var url = $"https://www.youtube.com/watch?v={video.Id}";
                _logger.LogTrace("Found active live stream: {Url}", url);
                return url;
            }

            // The first item has a duration — the channel is not currently live.
            break;
        }

        _logger.LogTrace("No active live stream found for channel {ChannelId}", channelId);
        return null;
    }
}
