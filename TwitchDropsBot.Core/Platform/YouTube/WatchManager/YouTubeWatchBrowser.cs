using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;
using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.YouTube.WatchManager;

/// <summary>
/// Opens a persistent Chromium profile for the user and navigates to live YouTube
/// streams.  The profile directory survives across bot restarts so the user only
/// needs to log in to their Google account once.
/// </summary>
public class YouTubeWatchBrowser : IYouTubeWatchManager, IAsyncDisposable
{
    private const string YoutubeLoginUrl =
        "https://accounts.google.com/ServiceLogin?service=youtube&continue=https%3A%2F%2Fwww.youtube.com";

    private const string YoutubeBaseUrl    = "https://www.youtube.com";
    private const string GoogleAccountsUrl = "https://accounts.google.com";

    private const int LoginTimeoutSeconds  = 300;

    private readonly ILogger _logger;
    private readonly IOptionsMonitor<BotSettings> _botSettings;

    private IBrowser? _browser;
    private IPage?    _page;
    private bool      _disposed;

    public YouTubeUser BotUser { get; }

    public YouTubeWatchBrowser(
        YouTubeUser user,
        ILogger logger,
        IOptionsMonitor<BotSettings> botSettings)
    {
        BotUser       = user;
        _logger       = logger;
        _botSettings  = botSettings;
    }

    // -------------------------------------------------------------------------
    // IWatchManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the browser (if not already open), verifies Google authentication and
    /// navigates to <paramref name="streamUrl"/>.  The page stays open until
    /// <see cref="Close"/> is called.
    /// </summary>
    /// <param name="streamUrl">Full YouTube watch URL.</param>
    /// <param name="channelId">Channel ID that the stream belongs to (informational).</param>
    public async Task WatchStreamAsync(string streamUrl, string channelId)
    {
        _disposed = false;

        await EnsureBrowserLaunchedAsync();
        await EnsureAuthenticatedAsync();

        if (_page != null)
        {
            _logger.LogDebug("Already watching a stream, skipping navigation");
            return;
        }

        _page = await _browser!.NewPageAsync();
        _logger.LogInformation("Navigating to stream {StreamUrl} (channel {ChannelId})", streamUrl, channelId);
        await _page.GoToAsync(streamUrl, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
            Timeout   = 30_000
        });

        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    public void Close()
    {
        if (_disposed) return;

        _logger.LogInformation("Closing YouTube watch browser for user {Login}", BotUser.Login);

        if (_page != null)
        {
            _ = _page.CloseAsync().ContinueWith(_ => { _page = null; });
        }

        if (_browser != null)
        {
            _ = _browser.CloseAsync().ContinueWith(_ => { _browser = null; });
        }

        _disposed = true;
    }

    // -------------------------------------------------------------------------
    // IAsyncDisposable
    // -------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        if (_page != null)
        {
            try { await _page.CloseAsync(); } catch { /* ignored */ }
            _page = null;
        }

        if (_browser != null)
        {
            try { await _browser.CloseAsync(); } catch { /* ignored */ }
            _browser = null;
        }

        GC.SuppressFinalize(this);
    }

    ~YouTubeWatchBrowser() => Close();

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task EnsureBrowserLaunchedAsync()
    {
        if (_browser != null) return;

        var profileDir = Path.Combine(
            AppContext.BaseDirectory,
            "profiles",
            $"youtube-{BotUser.Login}");

        Directory.CreateDirectory(profileDir);

        _logger.LogInformation(
            "Launching YouTube browser for user {Login} (profile: {ProfileDir})",
            BotUser.Login, profileDir);

        var launchOptions = new LaunchOptions
        {
            Headless    = _botSettings.CurrentValue.WatchBrowserHeadless,
            UserDataDir = profileDir,
            Args =
            [
                "--mute-audio",
                "--autoplay-policy=no-user-gesture-required",
                "--disable-infobars",
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-features=Translate,HardwareMediaKeyHandling",
                "--disable-blink-features=AutomationControlled",
                "--disable-background-timer-throttling",
                "--disable-backgrounding-occluded-windows",
            ],
            IgnoredDefaultArgs = ["--enable-automation"],
            DefaultViewport    = new ViewPortOptions { Width = 1280, Height = 720 },
        };

        var extra = new PuppeteerExtra();
        _browser  = await extra.Use(new StealthPlugin()).LaunchAsync(launchOptions);
    }

    /// <summary>
    /// Checks whether the user is already logged in to YouTube.  When running in
    /// non-headless mode and the user is not authenticated, it waits up to
    /// <see cref="LoginTimeoutSeconds"/> seconds for manual login.
    /// </summary>
    private async Task EnsureAuthenticatedAsync()
    {
        using var authPage = await _browser!.NewPageAsync();

        _logger.LogInformation("Checking YouTube authentication for user {Login}", BotUser.Login);

        await authPage.GoToAsync(YoutubeLoginUrl, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
            Timeout   = 15_000
        });

        await Task.Delay(TimeSpan.FromSeconds(3));

        var currentUrl = authPage.Url;

        if (currentUrl.StartsWith(YoutubeBaseUrl))
        {
            _logger.LogInformation("YouTube authentication check passed for user {Login}", BotUser.Login);
            await authPage.CloseAsync();
            return;
        }

        if (!currentUrl.StartsWith(GoogleAccountsUrl))
        {
            await authPage.CloseAsync();
            return;
        }

        // User is not logged in
        if (_botSettings.CurrentValue.WatchBrowserHeadless)
        {
            await authPage.CloseAsync();
            throw new InvalidOperationException(
                $"YouTube user '{BotUser.Login}' is not authenticated. " +
                "Set WatchBrowserHeadless to false and run the bot once to log in to your Google account.");
        }

        _logger.LogWarning(
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
                _logger.LogInformation(
                    "YouTube authentication successful for user {Login}", BotUser.Login);
                await authPage.CloseAsync();
                return;
            }
        }

        await authPage.CloseAsync();
        throw new InvalidOperationException(
            $"YouTube authentication timed out for user '{BotUser.Login}'. Please try again.");
    }
}
