using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.YouTube.Bot;

public class YouTubeBot : BaseBot<YouTubeUser>
{
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

        // Find first live channel
        string? liveStreamUrl = null;
        string? liveChannelId = null;

        foreach (var channelId in channelIds)
        {
            Logger.LogInformation("Checking channel {ChannelId} for live stream...", channelId);
            var url = await GetActiveLiveStreamWithFallbackAsync(channelId);
            if (url != null)
            {
                liveStreamUrl = url;
                liveChannelId = channelId;
                break;
            }
        }

        if (liveStreamUrl == null)
        {
            Logger.LogInformation("No live streams found on any configured channel.");
            throw new NoBroadcasterOrNoCampaignLeft();
        }

        Logger.LogInformation("Live stream found: {StreamUrl}", liveStreamUrl);

        // Open the stream in the browser
        try
        {
            await BotUser.WatchManager.WatchStreamAsync(liveStreamUrl, liveChannelId!);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to open YouTube stream");
            BotUser.WatchManager.Close();
            throw new StreamOffline();
        }

        // Poll until the stream ends
        var checkInterval = TimeSpan.FromSeconds(
            BotSettings.CurrentValue.YouTubeSettings.StreamCheckIntervalSeconds);

        while (true)
        {
            await Task.Delay(checkInterval);

            Logger.LogInformation("Re-checking if stream is still live on channel {ChannelId}...", liveChannelId);
            var stillLive = await BotUser.WatchManager.IsCurrentStreamLiveAsync();

            if (!stillLive)
            {
                Logger.LogInformation("Stream on channel {ChannelId} has ended.", liveChannelId);
                BotUser.WatchManager.Close();
                throw new StreamOffline();
            }

            Logger.LogInformation("Stream still live. Continuing to watch...");
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
}
