using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;

namespace TwitchDropsBot.Core.Object;

public enum BrowserType
{
    Chrome,
    Firefox
}

public class Browser : IDisposable
{
    public BrowserType Type { get; }
    public bool Headless { get; }
    public int? WindowWidth { get; }
    public int? WindowHeight { get; }
    public string? UserDataDir { get; }
    public IWebDriver WebDriver { get; private set; }

    public Browser(TwitchUser twitchUser, BrowserType type = BrowserType.Chrome, bool headless = true, int? windowWidth = null,
        int? windowHeight = null, string? userDataDir = null)
    {
        Type = type;
        Headless = headless;
        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
        UserDataDir = userDataDir + $"SeleniumData\\{twitchUser.Login}";

        WebDriver = CreateWebDriver();
    }

    private IWebDriver CreateWebDriver()
    {
        switch (Type)
        {
            case BrowserType.Chrome:
                var chromeOptions = new ChromeOptions();
                chromeOptions.AddArgument("--mute-audio"); // Mute audio
                if (Headless) chromeOptions.AddArgument("--headless=new");
                if (WindowWidth.HasValue && WindowHeight.HasValue)
                    chromeOptions.AddArgument($"--window-size={WindowWidth.Value},{WindowHeight.Value}");

                if (!string.IsNullOrEmpty(UserDataDir))
                    chromeOptions.AddArgument($"--user-data-dir={UserDataDir}");

                chromeOptions.AddArgument("--mute-audio");
                chromeOptions.AddArgument("--disable-infobars");
                chromeOptions.AddArgument("--no-sandbox");
                chromeOptions.AddArgument("--disable-login-animations");
                chromeOptions.AddArgument("--disable-modal-animations");
                chromeOptions.AddArgument("--no-sync");
                chromeOptions.AddArgument("--disable-sync");
                chromeOptions.AddArgument("--disable-renderer-backgrounding");
                chromeOptions.AddArgument("--no-default-browser-check");
                chromeOptions.AddArgument("--disable-default-apps");
                chromeOptions.AddArgument("--disable-component-update");
                chromeOptions.AddArgument("--disable-setuid-sandbox");
                chromeOptions.AddArgument("--disable-breakpad");
                chromeOptions.AddArgument("--disable-crash-reporter");
                chromeOptions.AddArgument("--disable-speech-api");
                chromeOptions.AddArgument("--no-zygote");
                chromeOptions.AddArgument("--disable-features=HardwareMediaKeyHandling");
                chromeOptions.AddArgument(
                    "--disable-blink-features=AutomationControlled,IdleDetection,CSSDisplayAnimation");
                chromeOptions.AddArgument("--disable-dev-shm-usage");
                
                chromeOptions.AddExcludedArgument("enable-automation");
                chromeOptions.AddAdditionalOption("useAutomationExtension", false);
                chromeOptions.AddArgument("--disable-features=IsolateOrigins,site-per-process");
                var driver = new ChromeDriver(chromeOptions);
                
                ((IJavaScriptExecutor)driver).ExecuteScript(
                    "Object.defineProperty(navigator, 'webdriver', {get: () => undefined})"
                );

                return driver;

            case BrowserType.Firefox:
                var firefoxOptions = new FirefoxOptions();
                firefoxOptions.SetPreference("media.volume_scale", "0.0");
                if (Headless) firefoxOptions.AddArgument("--headless");
                if (WindowWidth.HasValue && WindowHeight.HasValue)
                    firefoxOptions.AddArgument($"--width={WindowWidth.Value}");
                firefoxOptions.AddArgument($"--height={WindowHeight.Value}");
                return new FirefoxDriver(firefoxOptions);

            default:
                throw new NotSupportedException("Web browser not supported.");
        }
    }
    
    public void Dispose()
    {
        try
        {
            WebDriver?.Close();
        }
        catch (System.Exception ex)
        {
            SystemLogger.Error("[BROWSER] Error while closing the browser: " + ex.Message);
        }
        
        WebDriver?.Quit();
        WebDriver?.Dispose();
    }
}