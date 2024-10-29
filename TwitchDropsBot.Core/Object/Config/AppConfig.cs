using System.Text.Encodings.Web;
using System.Text.Json;

namespace TwitchDropsBot.Core.Object.Config;

public class AppConfig
{
    public List<ConfigUser> Users { get; set; }
    public List<string> FavouriteGames { get; set; }
    public bool OnlyFavouriteGames { get; set; }
    public bool LaunchOnStartup { get; set; }
    public bool MinimizeInTray{ get; set; }
    public bool OnlyConnectedAccounts { get; set; }
    public string? WebhookURL { get; set; }
    public static TwitchClient TwitchClient { get; } = TwitchClientType.ANDROID_APP;

    public AppConfig()
    {
        Users = new List<ConfigUser>();
        FavouriteGames = new List<string>();
        OnlyFavouriteGames = false;
        LaunchOnStartup = false;
        MinimizeInTray = true;
    }

    public static void SaveConfig(AppConfig config)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var jsonString = JsonSerializer.Serialize(config, options);
        var filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
        File.WriteAllText(filePath, jsonString);
    }

    public static AppConfig GetConfig()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "config.json");

        if (!File.Exists(filePath))
        {
            return new AppConfig();
        }

        var jsonString = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<AppConfig>(jsonString);
    }

}