using Microsoft.Playwright;
using TwitchDropsBot.Core.WatchManager;

namespace TwitchDropsBot.Core.Object;

public enum BrowserType
{
    Chromium,
    Firefox,
    Webkit
}

public class Browser : IAsyncDisposable
{
    public BrowserType Type { get; }
    public bool Headless { get; }
    public int? WindowWidth { get; }
    public int? WindowHeight { get; }
    public string? UserDataDir { get; }

    private IPlaywright? _playwright;
    public IBrowser? BrowserInstance { get; private set; }
    public IPage? Page { get; private set; }
    private bool _disposed = false;

    public Browser(TwitchUser twitchUser, BrowserType type = BrowserType.Chromium, bool headless = true,
        int? windowWidth = 1280,
        int? windowHeight = 720, string? userDataDir = null)
    {
        Type = type;
        Headless = headless;
        WindowWidth = windowWidth;
        WindowHeight = windowHeight;

        if (!string.IsNullOrEmpty(userDataDir))
            UserDataDir = userDataDir + $"PlaywrightData\\{twitchUser.Login}";
        else
            UserDataDir = null;

        InitializeAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();

        try
        {
            BrowserInstance = await CreateBrowserInstanceAsync();
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
        {
            SystemLogger.Info("[BROWSER] Installation of Playwright...");

            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "--with-deps", "chromium" });
            if (exitCode != 0)
            {
                throw new System.Exception($"Error when installing playwright (code {exitCode})");
            }

            SystemLogger.Info("[BROWSER] Install finished");
            BrowserInstance = await CreateBrowserInstanceAsync();
        }

        Page = await CreatePageAsync();
    }

    private async Task<IBrowser> CreateBrowserInstanceAsync()
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = Headless,
            SlowMo = 50,
            Channel = "msedge"
        };

        if (!string.IsNullOrEmpty(UserDataDir))
        {
            launchOptions.Args = new[] { $"--user-data-dir={UserDataDir}" };
        }

        switch (Type)
        {
            case BrowserType.Chromium:
                var args = new List<string>
                {
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
                    "--disable-dev-shm-usage",
                    "--disable-features=IsolateOrigins,site-per-process"
                };

                if (launchOptions.Args != null)
                    args.AddRange(launchOptions.Args);

                launchOptions.Args = args.ToArray();
                return await _playwright.Chromium.LaunchAsync(launchOptions);

            case BrowserType.Firefox:
                return await _playwright.Firefox.LaunchAsync(launchOptions);

            case BrowserType.Webkit:
                return await _playwright.Webkit.LaunchAsync(launchOptions);

            default:
                throw new NotSupportedException("Web browser not supported.");
        }
    }

    private async Task<IPage> CreatePageAsync()
    {
        if (BrowserInstance == null)
        {
            throw new InvalidOperationException("Browser not initialized");
        }

        var page = await BrowserInstance.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = WindowWidth ?? 1280,
                Height = WindowHeight ?? 720
            }
        });

        await page.AddInitScriptAsync(@"
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });
        ");

        return page;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                if (Page != null)
                {
                    await Page.CloseAsync();
                    Page = null;
                }
            }
            catch (System.Exception ex)
            {
                SystemLogger.Error("[BROWSER] Error while closing the page: " + ex.Message);
            }

            try
            {
                if (BrowserInstance != null)
                {
                    await BrowserInstance.CloseAsync();
                    BrowserInstance = null;
                }
            }
            catch (System.Exception ex)
            {
                SystemLogger.Error("[BROWSER] Error while closing the browser: " + ex.Message);
            }

            _playwright?.Dispose();
            _playwright = null;

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~Browser()
    {
        _ = DisposeAsync();
    }
}