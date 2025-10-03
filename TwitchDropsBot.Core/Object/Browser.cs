using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace TwitchDropsBot.Core.Object;

public class Browser
{
    public TwitchUser TwitchUser { get; }
    private bool headless;
    private IBrowser? _browser;
    public IBrowser? PuppeteerBrowser => _browser;

    public Browser(TwitchUser twitchUser, bool headless)
    {
        TwitchUser = twitchUser;
        this.headless = headless;
    }

    private async Task SetupBrowserAsync()
    {
        var fetcher = new BrowserFetcher();
        var installed = fetcher.GetInstalledBrowsers();
        if (!installed.Any())
        {
            await fetcher.DownloadAsync();
        }
    }

    public async Task InitAsync()
    {
        await SetupBrowserAsync();

        var launchOptions = new LaunchOptions
        {
            Headless = this.headless,
            Args =
            [
                "--mute-audio",
                "--disable-infobars",
                "--no-sandbox",
                "--disable-login-animations",
                "--disable-modal-animations",
                "--no-sync",
                "--disable-sync",
                "--disable-renderer-backgrounding",
                "--no-default-browser-check",
                "--disable-default-apps",
                "--disable-component-update",
                "--disable-setuid-sandbox",
                "--disable-breakpad",
                "--disable-crash-reporter",
                "--disable-speech-api",
                "--no-zygote",
                "--disable-features=HardwareMediaKeyHandling",
                "--disable-blink-features=AutomationControlled,IdleDetection,CSSDisplayAnimation",
            ],
            IgnoredDefaultArgs = ["--enable-automation", "--hide-scrollbars", "--enable-blink-features=IdleDetection"],
            DefaultViewport = new ViewPortOptions
            {
                Width = 960,
                Height = 1000
            },
            UserDataDir = "./BrowserData/" + TwitchUser.Login
        };
        
        _browser = await Puppeteer.LaunchAsync(launchOptions);
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            try
            {
                await _browser.CloseAsync();
                await _browser.DisposeAsync();
            }
            catch (System.Exception ex)
            {
                TwitchUser.Logger.Info("[BROWSER] CloseAsync failed, forcing kill...");
            }
            finally
            {
                try
                {
                    if (!_browser.Process.HasExited)
                    {
                        _browser.Process.Kill(true);
                    }
                }
                catch (System.Exception ex)
                {
                    TwitchUser.Logger.Info("[BROWSER] Failed to kill Chromium process");
                }

                _browser = null;
            }
        }
    }
}