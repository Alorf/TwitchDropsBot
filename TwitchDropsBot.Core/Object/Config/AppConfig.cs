using System.Text.Encodings.Web;
using System.Text.Json;

namespace TwitchDropsBot.Core.Object.Config;

public class AppConfig
{
    private static AppConfig? _instance;
    public List<ConfigUser> Users { get; set; }
    public List<string> FavouriteGames { get; set; }
    public List<string> AvoidCampaign { get; set; }
    public List<string> WatchSpecificStreamer { get; set; }
    public bool OnlyFavouriteGames { get; set; }
    public bool LaunchOnStartup { get; set; }
    public bool MinimizeInTray { get; set; }
    public bool OnlyConnectedAccounts { get; set; }
    public int LogLevel { get; set; }
    public string? WebhookURL { get; set; }
    public double waitingSeconds { get; set; }
    public int AttemptToWatch { get; set; }
    public bool headless { get; set; }
    public string WatchManager { get; set; }
    public static TwitchClient TwitchClient { get; } = TwitchClientType.ANDROID_APP;

    public static AppConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = LoadConfig();
            }

            return _instance;
        }
    }

    public AppConfig()
    {
        Users = new List<ConfigUser>();
        FavouriteGames = new List<string>();
        AvoidCampaign = new List<string>();
        WatchSpecificStreamer = new List<string>();
        OnlyFavouriteGames = false;
        LaunchOnStartup = false;
        MinimizeInTray = true;
        OnlyConnectedAccounts = false;
        waitingSeconds = 60*5;
        LogLevel = 0;
        AttemptToWatch = 3;
        headless = true;
        WatchManager = "WatchRequest";
    }

    private static AppConfig LoadConfig()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "config.json");

        if (!File.Exists(filePath))
        {
            return new AppConfig();
        }

        var jsonString = File.ReadAllText(filePath);

        return JsonSerializer.Deserialize<AppConfig>(jsonString);
    }

    public void SaveConfig()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var jsonString = JsonSerializer.Serialize(this, options);
        var filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
        File.WriteAllText(filePath, jsonString);
    }

    public AppConfig GetConfig()
    {
        var config = LoadConfig();
        SaveConfig();
        return config;
    }
}