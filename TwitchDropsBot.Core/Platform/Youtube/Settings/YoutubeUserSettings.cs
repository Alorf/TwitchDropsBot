using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Youtube.Settings;

public class YoutubeUserSettings : BaseUserSettings
{
    public string Name { get; set; }
    public string Cookies { get; set; }
}