using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.YouTube.Bot;

public class YouTubeBot : BaseBot<YouTubeUser>
{
    /// <summary>
    /// Channels confirmed offline in the current cycle.
    /// Reset when all configured channels have been exhausted (mirrors
    /// <c>finishedCampaigns</c> in <see cref="TwitchBot"/>).
    /// </summary>
    private readonly List<string> _offlineChannels = new();

    public YouTubeBot(
        YouTubeUser user,
        ILogger logger,
        NotificationService notificationService,
        IOptionsMonitor<BotSettings> botSettings)
        : base(user, logger, notificationService, botSettings)
    {
    }

    public override List<string> GetUserFavoriteGames() => new List<string>();

    protected override async Task StartAsync()
    {
        var channelIds = GetChannelIds();

        await BotUser.WatchManager.EnsureAuthenticatedAsync();

        if (channelIds.Count == 0)
        {
            Logger.LogError("No YouTube channel IDs configured for user {Login}. " +
                            "Add channel IDs to YouTubeSettings.ChannelIds or YouTubeUserSettings.ChannelIds.",
                BotUser.Login);
            throw new NoBroadcasterOrNoCampaignLeft();
        }

        BotUser.Status = BotStatus.Seeking;

        // If all channels were marked offline in the previous cycle, reset so we retry them all.
        var channelsToTry = channelIds.Except(_offlineChannels).ToList();
        if (channelsToTry.Count == 0)
        {
            Logger.LogInformation("All channels were offline last cycle, resetting channel list.");
            _offlineChannels.Clear();
            channelsToTry = new List<string>(channelIds);
        }

        // Mirrors the do-while broadcaster-selection loop in TwitchBot.
        while (channelsToTry.Count > 0)
        {
            // Find first live channel from the remaining list.
            string? liveStreamUrl = null;
            string? liveChannelId = null;

            foreach (var channelId in channelsToTry.ToList())
            {
                Logger.LogInformation("Checking channel {ChannelId} for live stream...", channelId);
                var url = await GetActiveLiveStreamWithFallbackAsync(channelId);
                if (url != null)
                {
                    liveStreamUrl = url;
                    liveChannelId = channelId;
                    break;
                }

                Logger.LogInformation("Channel {ChannelId} is not live, skipping.", channelId);
                channelsToTry.Remove(channelId);
                _offlineChannels.Add(channelId);
            }

            if (liveStreamUrl == null || liveChannelId == null)
            {
                Logger.LogInformation("No live streams found on any configured channel.");
                throw new NoBroadcasterOrNoCampaignLeft();
            }

            var videoId = ExtractVideoId(liveStreamUrl);
            if (videoId == null)
            {
                // URL is malformed — this is a parsing error, not a channel-offline condition.
                // Abort this cycle so the channel is retried on the next StartAsync call.
                Logger.LogError("Could not extract video ID from URL {StreamUrl}", liveStreamUrl);
                throw new NoBroadcasterOrNoCampaignLeft();
            }

            Logger.LogInformation("Live stream found: {StreamUrl}", liveStreamUrl);
            BotUser.Status = BotStatus.Watching;
            Logger.LogInformation("Watching channel {ChannelId} | {StreamUrl}", liveChannelId, liveStreamUrl);

            try
            {
                await WatchStreamAsync(liveStreamUrl, liveChannelId, videoId);
            }
            catch (StreamOffline)
            {
                Logger.LogInformation(
                    "Stream on channel {ChannelId} has ended, looking for another channel.", liveChannelId);
                channelsToTry.Remove(liveChannelId);
                _offlineChannels.Add(liveChannelId);
                BotUser.Status = BotStatus.Seeking;
            }
        }

        Logger.LogInformation("No more channels to watch.");
        throw new NoBroadcasterOrNoCampaignLeft();
    }

    // -------------------------------------------------------------------------
    // Watching loop (mirrors TwitchBot.WatchStreamAsync)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the stream in the browser and polls liveness until the stream ends.
    /// Includes a periodic browser health refresh that mirrors the stuck-counter
    /// browser reset in <see cref="TwitchBot"/>.
    /// </summary>
    private async Task WatchStreamAsync(string streamUrl, string channelId, string videoId)
    {
        var apiFailureCounter   = 0;
        var periodicRefreshCounter = 0;
        var checkInterval = TimeSpan.FromSeconds(
            BotSettings.CurrentValue.YouTubeSettings.StreamCheckIntervalSeconds);

        try
        {
            await BotUser.WatchManager.WatchStreamAsync(streamUrl, channelId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to open YouTube stream");
            BotUser.WatchManager.Close();
            throw new StreamOffline();
        }

        while (true)
        {
            await Task.Delay(checkInterval);

            Logger.LogInformation("Re-checking if video {VideoId} is still live...", videoId);

            bool stillLive;
            try
            {
                stillLive = await BotUser.YouTubeRepository.IsVideoLiveAsync(videoId);
                apiFailureCounter = 0;
            }
            catch (Exception ex)
            {
                apiFailureCounter++;
                Logger.LogWarning(ex,
                    "Failed to check stream status for {VideoId} ({Count}/5), will retry.",
                    videoId, apiFailureCounter);

                if (apiFailureCounter >= 5)
                {
                    Logger.LogError(
                        "Too many consecutive failures checking stream status for {VideoId}. Restarting.",
                        videoId);
                    BotUser.WatchManager.Close();
                    throw new StreamOffline();
                }

                continue;
            }

            if (!stillLive)
            {
                Logger.LogInformation("Stream on channel {ChannelId} has ended.", channelId);
                BotUser.WatchManager.Close();
                throw new StreamOffline();
            }

            // Periodic browser health refresh — mirrors the stuck-counter browser reset in TwitchBot
            // (30 consecutive checks without issue triggers a clean reopen).
            periodicRefreshCounter++;
            if (periodicRefreshCounter >= 30)
            {
                Logger.LogInformation(
                    "Performing periodic browser refresh for channel {ChannelId}.", channelId);
                BotUser.WatchManager.Close();
                periodicRefreshCounter = 0;
                try
                {
                    await BotUser.WatchManager.WatchStreamAsync(streamUrl, channelId);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to re-open stream during periodic refresh");
                    throw new StreamOffline();
                }
            }
            else
            {
                Logger.LogInformation("Stream still live. Continuing to watch...");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to detect a live stream via the lightweight InnerTube HTTP API first.
    /// Falls back to the authenticated browser check when InnerTube fails or returns
    /// an unexpected response.
    /// </summary>
    private async Task<string?> GetActiveLiveStreamWithFallbackAsync(string channelId)
    {
        try
        {
            var url = await BotUser.YouTubeRepository.GetActiveLiveStreamAsync(channelId);
            if (url != null)
                return url;

            // InnerTube returned null (channel not live) — no need for the browser check.
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "InnerTube check failed for channel {ChannelId}, falling back to browser",
                channelId);
        }

        // Fallback: use the authenticated Playwright browser.
        return await BotUser.WatchManager.GetActiveLiveStreamUrlAsync(channelId);
    }

    /// <summary>
    /// Returns the channel IDs for this user: per-user list when non-empty, otherwise
    /// the global list from <see cref="BotSettings.YouTubeSettings"/>.
    /// </summary>
    private List<string> GetChannelIds()
    {
        var userSettings = BotSettings.CurrentValue.YouTubeSettings.YouTubeUsers
            .Find(u => u.Id == BotUser.Id);

        if (userSettings?.ChannelIds?.Count > 0)
            return userSettings.ChannelIds;

        return BotSettings.CurrentValue.YouTubeSettings.ChannelIds;
    }

    /// <summary>
    /// Extracts the YouTube video ID from a watch URL
    /// (e.g. <c>https://www.youtube.com/watch?v=VIDEOID</c>).
    /// Returns <c>null</c> when the URL does not contain a recognisable video ID.
    /// </summary>
    private static string? ExtractVideoId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return query["v"];
    }
}
