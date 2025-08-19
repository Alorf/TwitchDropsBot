using OpenQA.Selenium.DevTools;
using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.Core.WatchManager;

public class WatchBrowser : WatchManager, IDisposable
{
    private Browser? browser;
    private bool disposed = false;

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

        if (browser != null) return;

        browser = new Browser(twitchUser, BrowserType.Chrome, AppConfig.Instance.headless, 1280, 720, AppContext.BaseDirectory);
        
        disposed = false;
        browser.WebDriver.Navigate().GoToUrl("https://www.twitch.tv/");

        var cookieName = "auth-token";
        var cookies = browser.WebDriver.Manage().Cookies.AllCookies;
        var cookieExists = cookies.Any(c => c.Name == cookieName);

        if (!cookieExists)
        {
            var cookie = new OpenQA.Selenium.Cookie(cookieName, twitchUser.ClientSecret, ".twitch.tv", "/",
                DateTime.Now.AddDays(7));
            browser.WebDriver.Manage().Cookies.AddCookie(cookie);
        }
        
        browser.WebDriver.Navigate().Refresh();
        browser.WebDriver.Navigate().GoToUrl($"https://www.twitch.tv/{broadcaster.Login}");

        try
        {
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(browser.WebDriver, TimeSpan.FromSeconds(10));
            var button = wait.Until(driver =>
                driver.FindElement(OpenQA.Selenium.By.CssSelector(
                    "button[data-a-target='content-classification-gate-overlay-start-watching-button']")));

            button.Click();
        }
        catch(System.Exception ex)
        {
            twitchUser.Logger.Error("[BROWSER] No classification button found, continuing...");
        }

        try
        {
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(browser.WebDriver, TimeSpan.FromSeconds(10));

            // 1. Settings
            var settingsButton = wait.Until(driver => driver.FindElement(OpenQA.Selenium.By.CssSelector("button[data-a-target='player-settings-button']")));
            settingsButton.Click();

            // 2. Quality
            var qualityButton = wait.Until(driver => driver.FindElement(OpenQA.Selenium.By.CssSelector("button[data-a-target='player-settings-menu-item-quality']")));
            qualityButton.Click();

            // 3. Lowest Quality
            var qualityMenu = wait.Until(driver => driver.FindElement(OpenQA.Selenium.By.CssSelector("div[data-a-target='player-settings-menu']")));
            var qualityOptions = qualityMenu.FindElements(OpenQA.Selenium.By.CssSelector("div[data-a-target='player-settings-submenu-quality-option'] label"));

            foreach (var option in qualityOptions)
            {
                if (option.Text.Contains("160p"))
                {
                    option.Click();
                    break;
                }
            }
        }
        catch (System.Exception ex)
        {
            twitchUser.Logger.Error(ex);
        }   
        
        await Task.Delay(TimeSpan.FromSeconds(10));
    }

    public override void Close()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (!disposed)
        {
            browser?.Dispose();
            browser = null;
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~WatchBrowser()
    {
        Dispose();
    }
}