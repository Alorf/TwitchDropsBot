using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.YouTube.Settings;

public class YouTubeUserSettings : BaseUserSettings
{
    /// <summary>
    /// Optional per-user channel IDs.  When non-empty, these are used instead of the
    /// global <see cref="YouTubeSettings.ChannelIds"/> list.
    /// </summary>
    public List<string> ChannelIds { get; set; } = new List<string>();

    /// <summary>
    /// Enables yt-dlp cookie based authentication for this user.
    /// When enabled, <see cref="CookiesFilePath"/> must point to a valid Netscape cookie file.
    /// </summary>
    public bool CookieLogin { get; set; } = false;

    /// <summary>
    /// Path to a Netscape-format cookies file (used with yt-dlp <c>--cookies</c>).
    /// </summary>
    public string? CookiesFilePath { get; set; }
}
