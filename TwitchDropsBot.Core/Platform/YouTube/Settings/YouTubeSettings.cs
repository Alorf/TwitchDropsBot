namespace TwitchDropsBot.Core.Platform.YouTube.Settings;

public class YouTubeSettings
{
    public List<YouTubeUserSettings> YouTubeUsers { get; init; } = new List<YouTubeUserSettings>();

    /// <summary>
    /// YouTube channel IDs to watch (e.g. "UCiAInBL9kUzz1XRxk66v-gw" for OWL).
    /// Applied globally to all users unless overridden per-user.
    /// </summary>
    public List<string> ChannelIds { get; set; } = new List<string>();

    /// <summary>
    /// How often (in seconds) the bot checks whether a configured channel has gone live.
    /// Defaults to 5 minutes.
    /// </summary>
    public int StreamCheckIntervalSeconds { get; set; } = 300;
}
