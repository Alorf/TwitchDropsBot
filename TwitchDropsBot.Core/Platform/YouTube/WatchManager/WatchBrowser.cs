using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.YouTube.Bot;

namespace TwitchDropsBot.Core.Platform.YouTube.WatchManager;

public class WatchBrowser : WatchBrowser<YouTubeUser, string, string>, IYouTubeWatchManager
{
    public WatchBrowser(YouTubeUser botUser, ILogger baseLogger, BrowserService browserService)
        : base(botUser, baseLogger, browserService)
    {
    }

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

    public Task EnsureBrowserLaunchedAsync() => throw new NotImplementedException();

    private const string YoutubeLoginUrl =
        "https://accounts.google.com/ServiceLogin?service=youtube&continue=https%3A%2F%2Fwww.youtube.com";

    private const string YoutubeBaseUrl    = "https://www.youtube.com";
    private const string GoogleAccountsUrl = "https://accounts.google.com";
    private const int    LoginTimeoutSeconds = 300;

    public async Task EnsureAuthenticatedAsync()
    {
        Page = await BrowserService.AddUserAsync(BotUser);

        // Open a temporary page within the same browser context
        var authPage = await Page.Context.NewPageAsync();

        Logger.LogInformation("Checking YouTube authentication for user {Login}", BotUser.Login);

        await authPage.GotoAsync(YoutubeLoginUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout   = 15_000
        });

        await Task.Delay(TimeSpan.FromSeconds(3));

        if (authPage.Url.StartsWith(YoutubeBaseUrl))
        {
            Logger.LogInformation("YouTube authentication check passed for user {Login}", BotUser.Login);
            await authPage.CloseAsync();
            return;
        }

        if (!authPage.Url.StartsWith(GoogleAccountsUrl))
        {
            await authPage.CloseAsync();
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

            if (authPage.Url.StartsWith(YoutubeBaseUrl))
            {
                Logger.LogInformation("YouTube authentication successful for user {Login}", BotUser.Login);
                await authPage.CloseAsync();
                await BrowserService.SaveStorageStateAsync(BotUser);
                return;
            }
        }

        await authPage.CloseAsync();
        throw new InvalidOperationException(
            $"YouTube authentication timed out for user '{BotUser.Login}'. Please try again.");
    }
}