using PuppeteerSharp;
using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;
using Browser = TwitchDropsBot.Core.Object.Browser;

namespace TwitchDropsBot.Core.WatchManager;

public class WatchBrowser : WatchManager, IAsyncDisposable
{
    private bool _disposed = false;
    private Browser? _browser;

    public WatchBrowser(TwitchUser twitchUser, CancellationTokenSource cancellationTokenSource) : base(twitchUser,
        cancellationTokenSource)
    {
    }

    public override async Task WatchStreamAsync(AbstractBroadcaster? broadcaster)
    {
        // Check if stream still live, if not throw error and close
        if (broadcaster != null)
        {
            var tempBroadcaster = await twitchUser.GqlRequest.FetchStreamInformationAsync(broadcaster.Login);

            if (tempBroadcaster != null)
            {
                if (tempBroadcaster.Stream == null)
                {
                    throw new StreamOffline();
                }
            }
        }

        cancellationTokenSource = new CancellationTokenSource();

        if (_browser != null) return;
        
        _browser = new Browser(
            twitchUser,
            AppConfig.Instance.WatchManagerConfig.headless
        );
        await _browser.InitAsync();
        
        var mainBrowser = _browser.PuppeteerBrowser;
        
        if (mainBrowser == null)
        {
            throw new System.Exception("Browser not initialized");
        }
        
        var page = await mainBrowser.NewPageAsync();

        await page.GoToAsync("https://www.twitch.tv/");

        await page.SetCookieAsync(
            new CookieParam()
            {
                Name = "auth-token",
                Value = twitchUser.ClientSecret,
                Domain = ".twitch.tv",
                Path = "/",
                Expires = DateTimeOffset.Now.AddDays(7).ToUnixTimeSeconds()
            });


        await page.ReloadAsync();

        await page.GoToAsync($"https://www.twitch.tv/{broadcaster.Login}");

        // If classification overlay
        try
        {
            await page.WaitForSelectorAsync("button[data-a-target='content-classification-gate-overlay-start-watching-button']", new() { Timeout = 10000 });
            await page.ClickAsync("button[data-a-target='content-classification-gate-overlay-start-watching-button']");
        }
        catch (System.Exception ex)
        {
            twitchUser.Logger.Info("[BROWSER] No classification button found, continuing...");
        }

        // Quality settings
        try
        {
            await page.WaitForSelectorAsync("button[data-a-target='player-settings-button']", new WaitForSelectorOptions { Timeout = 10000 });
            await page.ClickAsync("button[data-a-target='player-settings-button']");

            await page.WaitForSelectorAsync("button[data-a-target='player-settings-menu-item-quality']", new WaitForSelectorOptions { Timeout = 10000 });
            await page.ClickAsync("button[data-a-target='player-settings-menu-item-quality']");

            await page.WaitForSelectorAsync("div[data-a-target='player-settings-menu']", new WaitForSelectorOptions { Timeout = 10000 });

            var lowQualityOption = await page.QuerySelectorAllAsync("div[data-a-target='player-settings-submenu-quality-option'] label");
            var lastOption = lowQualityOption.LastOrDefault();
            if (lastOption != null)
            {
                await lastOption.ClickAsync();
            }
        }
        catch (System.Exception ex)
        {
            twitchUser.Logger.Error($"[BROWSER] Quality settings error: {ex.Message}");
        }

        await Task.Delay(TimeSpan.FromSeconds(10), cancellationTokenSource.Token);
    }

    public override async Task<DropCurrentSession?> FakeWatchAsync(AbstractBroadcaster broadcaster, int tryCount = 3)
    {
        // Watch for 20*trycount seconds
        var startTime = DateTime.Now;
        await WatchStreamAsync(broadcaster);

        while (true)
        {
            CheckCancellation();
            var timeElapsed = DateTime.Now - startTime;
            if (timeElapsed.TotalSeconds > 20 * tryCount)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Close();

        return await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
    }

    public override void Close()
    {
        _ = DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        if (_browser != null)
        {
            await _browser.DisposeAsync();
            _browser = null;
        }
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WatchBrowser()
    {
        Close();
    }
}