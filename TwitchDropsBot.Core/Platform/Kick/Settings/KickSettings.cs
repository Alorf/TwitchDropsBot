using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Settings;

namespace TwitchDropsBot.Core.Platform.Kick.Settings;

public class KickSettings
{
    public List<KickUserSettings> KickUsers { get; init; } = new List<KickUserSettings>();
    // public KickAppSettings KickApp { get; set; } = new KickAppSettings();
    public string WatchManager { get; set; } = WatchManagerType.WatchRequest;
}