using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Settings;

namespace TwitchDropsBot.Core.Platform.Youtube.Settings;

public class YoutubeSettings
{
    public List<YoutubeUserSettings> YoutubeUsers { get; init; } = new List<YoutubeUserSettings>();
    public string WatchManager { get; set; } = WatchManagerType.WatchBrowser;
}