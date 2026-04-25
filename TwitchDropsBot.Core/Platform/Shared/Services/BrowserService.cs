using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

public sealed class BrowserService
{
    private readonly Dictionary<string, IBrowserContext> _userContexts = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IBrowser? _browser;
    private IPlaywright? _playwright;
    private bool _isInitialized;
    private readonly IOptionsMonitor<BotSettings> _botSettings;
    private readonly ILogger<BotUser> _logger;

    public BrowserService(IOptionsMonitor<BotSettings> botSettings, ILogger<BotUser> logger)
    {
        _botSettings = botSettings;
        _logger = logger;
    }

    private async Task InitializeBrowserAsync()
    {
        if (_isInitialized) return;

        // Install Playwright browsers if needed (equivalent of BrowserFetcher)
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}.");

        _playwright = await Playwright.CreateAsync();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = _botSettings.CurrentValue.WatchBrowserHeadless,
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
                "--disable-backgrounding-occluded-windows",
                "--disable-background-timer-throttling",
                "--disable-ipc-flooding-protection"
            ],
        };

        _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
        _isInitialized = true;
    }

    public async Task<IPage> AddUserAsync(BotUser user)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_isInitialized)
            {
                _logger.LogInformation("[BROWSER SERVICE] Creating shared browser...");
                await InitializeBrowserAsync();
                _logger.LogInformation("[BROWSER SERVICE] Shared browser created successfully");
            }

            var userKey = user.Id;

            if (_userContexts.TryGetValue(userKey, out var existingContext))
            {
                _logger.LogInformation("[BROWSER SERVICE] Reusing existing context for {Login}", user.Login);
                return await existingContext.NewPageAsync();
            }

            _logger.LogInformation("[BROWSER SERVICE] Creating new isolated context for {Login}", user.Login);

            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            });

            _userContexts[userKey] = context;

            var page = await context.NewPageAsync();

            _logger.LogInformation(
                "[BROWSER SERVICE] Context created - Total: {Count} active context(s)", _userContexts.Count);

            return page;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveUserAsync(BotUser user)
    {
        await _lock.WaitAsync();
        try
        {
            var userKey = user.Id;

            if (!_userContexts.TryGetValue(userKey, out var context))
            {
                _logger.LogInformation("[BROWSER SERVICE] No context to remove for {Login}", user.Login);
                return;
            }

            _logger.LogInformation("[BROWSER SERVICE] Closing context for {Login}", user.Login);
            await context.CloseAsync();
            _userContexts.Remove(userKey);

            _logger.LogInformation(
                "[BROWSER SERVICE] Context closed - Remaining: {Count} active context(s)", _userContexts.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var context in _userContexts.Values)
                await context.CloseAsync();

            _userContexts.Clear();

            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }

            _playwright?.Dispose();
            _playwright = null;

            _isInitialized = false;
        }
        finally
        {
            _lock.Release();
        }
    }
}