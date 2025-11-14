using TwitchDropsBot.Core.Platform.Kick.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Settings;

namespace TwitchDropsBot.Core.Platform.Shared.Settings;

public class BotSettings
{
    public TwitchSettings TwitchSettings { get; set; } = new TwitchSettings();
    public KickSettings KickSettings { get; set; } = new KickSettings();
    public List<string> FavouriteGames { get; set; } = new List<string>();
    public bool LaunchOnStartup { get; set; } = false;
    public int LogLevel { get; set; } = 0;
    public string? WebhookURL { get; set; } = string.Empty;
    public double waitingSeconds { get; set; } = TimeSpan.FromMinutes(5).TotalSeconds;
    public int AttemptToWatch { get; set; } = 5;
    public bool WatchBrowserHeadless { get; set; } = false;
    public bool MinimizeInTray { get; set; } = false;
}