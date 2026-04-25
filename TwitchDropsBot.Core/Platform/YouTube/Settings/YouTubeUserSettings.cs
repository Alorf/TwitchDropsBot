using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.YouTube.Settings;

public class YouTubeUserSettings : BaseUserSettings
{
    /// <summary>
    /// Optional per-user channel IDs.  When non-empty, these are used instead of the
    /// global <see cref="YouTubeSettings.ChannelIds"/> list.
    /// </summary>
    public List<string> ChannelIds { get; set; } = new List<string>();
}
