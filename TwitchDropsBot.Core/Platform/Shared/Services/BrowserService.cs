using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

public sealed class BrowserService
{
    private readonly Dictionary<string, IBrowserContext> _userContexts = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IBrowser? _browser;
    private bool _isInitialized;
    private readonly IOptionsMonitor<BotSettings> _botSettings;
    private ILogger<BotUser> _logger;

    public BrowserService(IOptionsMonitor<BotSettings> botSettings, ILogger<BotUser> logger)
    {
        _botSettings = botSettings;
        _logger = logger;
    }
    
    private async Task InitializeBrowserAsync()
    {
        if (_isInitialized) return;
        
        var browserFetcher = new BrowserFetcher();
        var installed = browserFetcher.GetInstalledBrowsers();
        if (!installed.Any())
        {
            _logger.LogWarning("Can't find any installed browsers. Downloading latest headless chrome...");
            await browserFetcher.DownloadAsync();
            _logger.LogWarning("Downloaded latest headless chrome.");
        }
        
        var launchOptions = new LaunchOptions
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
            IgnoredDefaultArgs =
                ["--enable-automation", "--hide-scrollbars", "--enable-blink-features=IdleDetection"],
            DefaultViewport = new ViewPortOptions
            {
                Width = 1920,
                Height = 1080
            },
        };

        var extra = new PuppeteerExtra();

        var stealth = new StealthPlugin();
        
        _browser = await extra.Use(stealth).LaunchAsync(launchOptions);
        
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

            if (_userContexts.ContainsKey(userKey))
            {
                _logger.LogInformation($"[BROWSER SERVICE] Reusing existing context for {user.Login}");
                return await _userContexts[userKey].NewPageAsync();
            }

            _logger.LogInformation($"[BROWSER SERVICE] Creating new isolated context for {user.Login}");
            var context = await _browser!.CreateBrowserContextAsync();
            _userContexts[userKey] = context;
            
            var page = await context.NewPageAsync();
            
            _logger.LogInformation($"[BROWSER SERVICE] Context created - Total: {_userContexts.Count} active context(s)");
            
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
            
            if (!_userContexts.ContainsKey(userKey))
            {
                _logger.LogInformation($"[BROWSER SERVICE] No context to remove for {user.Login}");
                return;
            }

            _logger.LogInformation($"[BROWSER SERVICE] Closing context for {user.Login}");
            await _userContexts[userKey].CloseAsync();
            _userContexts.Remove(userKey);
            
            _logger.LogInformation($"[BROWSER SERVICE] Context closed - Remaining: {_userContexts.Count} active context(s)");
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
            if (_userContexts.Count > 0)
            {
                foreach (var context in _userContexts.Values)
                {
                    await context.CloseAsync();
                }
                _userContexts.Clear();
            }

            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }

            _isInitialized = false;
        }
        finally
        {
            _lock.Release();
        }
    }
}