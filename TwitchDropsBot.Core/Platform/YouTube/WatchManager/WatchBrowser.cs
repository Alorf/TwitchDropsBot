using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.YouTube.Bot;

namespace TwitchDropsBot.Core.Platform.YouTube.WatchManager;

public class WatchBrowser : WatchBrowser<YouTubeUser, string, string>, IYouTubeWatchManager
{
    private const string YoutubeLoginUrl =
        "https://accounts.google.com/ServiceLogin?service=youtube&continue=https%3A%2F%2Fwww.youtube.com";

    private const string YoutubeBaseUrl    = "https://www.youtube.com";
    private const string GoogleAccountsUrl = "https://accounts.google.com";
    private const int    LoginTimeoutSeconds = 300;

    public WatchBrowser(YouTubeUser botUser, ILogger baseLogger, BrowserService browserService)
        : base(botUser, baseLogger, browserService)
    {
    }

    // -------------------------------------------------------------------------
    // IYouTubeWatchManager – authentication
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task EnsureAuthenticatedAsync()
    {
        // Open a temporary page to verify / perform login.
        // Do NOT assign to Page so that WatchStreamAsync can set it later.
        var tempPage = await BrowserService.AddUserAsync(BotUser);
        try
        {
            Logger.LogInformation("Checking YouTube authentication for user {Login}", BotUser.Login);

            await tempPage.GotoAsync(YoutubeLoginUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout   = 15_000
            });

            await Task.Delay(TimeSpan.FromSeconds(3));

            if (tempPage.Url.StartsWith(YoutubeBaseUrl))
            {
                Logger.LogInformation("YouTube authentication check passed for user {Login}", BotUser.Login);
                return;
            }

            if (!tempPage.Url.StartsWith(GoogleAccountsUrl))
            {
                return;
            }

            Logger.LogWarning(
                "YouTube user {Login} is not logged in. " +
                "Please log in to your Google account in the browser window. " +
                "You have {Timeout} seconds.",
                BotUser.Login, LoginTimeoutSeconds);

            var deadline = DateTime.UtcNow.AddSeconds(LoginTimeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));

                if (tempPage.Url.StartsWith(YoutubeBaseUrl))
                {
                    Logger.LogInformation("YouTube authentication successful for user {Login}", BotUser.Login);
                    await BrowserService.SaveStorageStateAsync(BotUser);
                    return;
                }
            }

            throw new InvalidOperationException(
                $"YouTube authentication timed out for user '{BotUser.Login}'. Please try again.");
        }
        finally
        {
            await tempPage.CloseAsync();
        }
    }

    // -------------------------------------------------------------------------
    // IYouTubeWatchManager – live-stream detection
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<string?> GetActiveLiveStreamUrlAsync(string channelId)
    {
        Logger.LogTrace("Checking for active live stream on channel {ChannelId}", channelId);

        var tempPage = await BrowserService.AddUserAsync(BotUser);
        try
        {
            var liveUrl = $"https://www.youtube.com/@{channelId}/live";

            await tempPage.GotoAsync(liveUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout   = 15_000
            });

            // Small delay to allow any client-side redirects to settle.
            await Task.Delay(TimeSpan.FromSeconds(2));

            var videoElement = await tempPage.QuerySelectorAsync("video");
            if (videoElement != null)
            {
                // YouTube redirects /live → /watch?v=VIDEOID when the channel is live.
                var watchUrl = tempPage.Url;
                Logger.LogTrace("Found active live stream: {WatchUrl}", watchUrl);
                return watchUrl;
            }

            Logger.LogTrace("No active live stream found for channel {ChannelId}", channelId);
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to check live stream status for channel {ChannelId}", channelId);
            return null;
        }
        finally
        {
            await tempPage.CloseAsync();
        }
    }

    // -------------------------------------------------------------------------
    // IWatchManager – watch a stream
    // -------------------------------------------------------------------------

    public override async Task WatchStreamAsync(string streamUrl, string channelId)
    {
        _disposed = false;

        if (Page != null)
        {
            Logger.LogDebug("Already watching a stream, skipping navigation");
            return;
        }

        Page = await BrowserService.AddUserAsync(BotUser);
        Logger.LogInformation("Navigating to stream {StreamUrl} (channel {ChannelId})", streamUrl, channelId);

        await Page.GotoAsync(streamUrl);

        await Task.Delay(TimeSpan.FromSeconds(10));
    }
}
