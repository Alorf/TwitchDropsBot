using System.Text.Encodings.Web;
using System.Text.Json;
using System.IO;

namespace TwitchDropsBot.Core.Object.Config;

public class AppConfig
{
    private static AppConfig? _instance;
    private static readonly object _lock = new();
    private static FileSystemWatcher? _watcher;
    private static DateTime _lastRead = DateTime.MinValue;
    
    public List<UserConfig> Users { get; set; }
    public List<string> FavouriteGames { get; set; }
    public List<string> AvoidCampaign { get; set; }
    public bool OnlyFavouriteGames { get; set; }
    public bool LaunchOnStartup { get; set; }
    public bool MinimizeInTray { get; set; }
    public bool ForceTryWithTags { get; set; }
    public bool OnlyConnectedAccounts { get; set; }
    public int LogLevel { get; set; }
    public string? WebhookURL { get; set; }
    public double waitingSeconds { get; set; }
    public int AttemptToWatch { get; set; }
    
    public WatchManagerConfig WatchManagerConfig { get; set; }
    public static TwitchClient TwitchClient { get; } = TwitchClientType.ANDROID_APP;

    public static AppConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = LoadConfig();
                        SetupWatcher();
                    }
                }
            }
            return _instance;
        }
    }

    public AppConfig()
    {
        Users = new List<UserConfig>();
        FavouriteGames = new List<string>();
        AvoidCampaign = new List<string>();
        OnlyFavouriteGames = false;
        LaunchOnStartup = false;
        MinimizeInTray = true;
        ForceTryWithTags = false;
        OnlyConnectedAccounts = false;
        waitingSeconds = TimeSpan.FromMinutes(5).TotalSeconds;
        LogLevel = 0;
        AttemptToWatch = 3;
        
        WatchManagerConfig = new WatchManagerConfig();
        WatchManagerConfig.headless = true;
        WatchManagerConfig.WatchManager = "WatchRequest";
    }

    private static void SetupWatcher()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory);
        var configFileName = "config.json";
        
        _watcher = new FileSystemWatcher(configPath, configFileName);
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.Changed += OnConfigFileChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            if (DateTime.Now - _lastRead < TimeSpan.FromSeconds(1))
            {
                return;
            }
            _lastRead = DateTime.Now;
            
            try
            {
                // To avoid issues with file locks, wait a bit before reading
                Thread.Sleep(100); 
                _instance = LoadConfig();
                SystemLogger.Info("Configuration reloaded."); // Or use your logger
            }
            catch (System.Exception ex)
            {
                SystemLogger.Error($"Error reloading configuration: {ex.Message}");
            }
        }
    }

    private static AppConfig LoadConfig()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "config.json");

        if (!File.Exists(filePath))
        {
            return new AppConfig();
        }

        var jsonString = File.ReadAllText(filePath);

        return JsonSerializer.Deserialize<AppConfig>(jsonString) ?? new AppConfig();
    }

    public void SaveConfig()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
        }
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var jsonString = JsonSerializer.Serialize(this, options);
        var filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
        File.WriteAllText(filePath, jsonString);
        
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = true;
        }
    }
}