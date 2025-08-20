
using Microsoft.Playwright;
using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;
using BrowserType = TwitchDropsBot.Core.Object.BrowserType;

namespace TwitchDropsBot.Core.WatchManager;

public class WatchBrowser : WatchManager, IAsyncDisposable
{
    private Browser? _browser;
    private bool _disposed = false;

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
            BrowserType.Chromium, 
            AppConfig.Instance.headless, 
            1280, 
            720
        );
        
        _disposed = false;

        await _browser.Page.GotoAsync("https://www.twitch.tv/");

        await _browser.Page.Context.AddCookiesAsync(new[] {
            new Cookie {
                Name = "auth-token",
                Value = twitchUser.ClientSecret,
                Domain = ".twitch.tv",
                Path = "/",
                Expires = DateTimeOffset.Now.AddDays(7).ToUnixTimeSeconds()
            }
        });

        await _browser.Page.ReloadAsync();
        
        await _browser.Page.GotoAsync($"https://www.twitch.tv/{broadcaster.Login}");

        // If classification overlay
        try
        {
            await _browser.Page.WaitForSelectorAsync(
                "button[data-a-target='content-classification-gate-overlay-start-watching-button']", 
                new PageWaitForSelectorOptions { Timeout = 10000 });
            await _browser.Page.ClickAsync(
                "button[data-a-target='content-classification-gate-overlay-start-watching-button']");
        }
        catch(System.Exception ex)
        {
            twitchUser.Logger.Error("[BROWSER] No classification button found, continuing...");
        }

        // Quality settings
        try
        {
            await _browser.Page.WaitForSelectorAsync("button[data-a-target='player-settings-button']", 
                new PageWaitForSelectorOptions { Timeout = 10000 });
            await _browser.Page.ClickAsync("button[data-a-target='player-settings-button']");
            
            await _browser.Page.WaitForSelectorAsync("button[data-a-target='player-settings-menu-item-quality']",
                new PageWaitForSelectorOptions { Timeout = 10000 });
            await _browser.Page.ClickAsync("button[data-a-target='player-settings-menu-item-quality']");
            
            await _browser.Page.WaitForSelectorAsync("div[data-a-target='player-settings-menu']",
                new PageWaitForSelectorOptions { Timeout = 10000 });
            
            var lowQualityOption = await _browser.Page.QuerySelectorAllAsync(
                "div[data-a-target='player-settings-submenu-quality-option'] label");
            
            var lastOption = lowQualityOption.LastOrDefault();
            if (lastOption != null)
            {
                await lastOption.ClickAsync();
            }
            
        }
        catch (System.Exception ex)
        {
            twitchUser.Logger.Error(ex);
        }
        
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationTokenSource.Token);
    }

    public override void Close()
    {
        _ = DisposeAsync();
    }
    
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_browser != null)
            {
                await _browser.DisposeAsync();
                _browser = null;
            }
            
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~WatchBrowser()
    {
        Close();
    }
}