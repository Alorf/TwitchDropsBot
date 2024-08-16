using System.Text.Json;

namespace TwitchDropsBot.Core.Object;

public class AppConfig
{
    public List<ConfigUser> Users { get; set; }
    public List<string> FavouriteGames { get; set; }
    public bool OnlyFavouriteGames { get; set; }
    public bool LaunchOnStartup { get; set; }
    public bool MinimizeInTray{ get; set; }
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
            WriteIndented = true
        };

        var jsonString = JsonSerializer.Serialize(config, options);
        File.WriteAllText("config.json", jsonString);
    }

    public static AppConfig GetConfig()
    {
        if (!File.Exists("config.json"))
        {
            return new AppConfig();
        }

        var jsonString = File.ReadAllText("config.json");
        return JsonSerializer.Deserialize<AppConfig>(jsonString);
    }

}