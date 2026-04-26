using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;

namespace TwitchDropsBot.Core.Platform.YouTube.WatchManager;

/// <summary>
/// Watch-manager interface for the YouTube platform.
/// <para>
/// <c>TBroadcaster</c> = live stream URL (e.g. https://www.youtube.com/watch?v=…)<br/>
/// <c>TGame</c>        = YouTube channel ID being watched
/// </para>
/// </summary>
public interface IYouTubeWatchManager : IWatchManager<YouTubeUser, string, string>
{
    /// <summary>
    /// Checks whether the user is already logged in to YouTube.
    /// When running in non-headless mode and the user is not authenticated,
    /// waits up to 300 seconds for manual login.
    /// </summary>
    Task EnsureAuthenticatedAsync();

    /// <summary>
    /// Navigates to <c>https://www.youtube.com/@{channelId}/live</c> using the
    /// authenticated browser context and returns the live-stream watch URL when
    /// the channel is currently live, or <c>null</c> when it is not.
    /// </summary>
    Task<string?> GetActiveLiveStreamUrlAsync(string channelId);

    /// <summary>
    /// Checks whether the stream currently open in the watching browser is still live,
    /// by looking for the <c>.ytp-live-badge</c> element that YouTube displays in the
    /// player controls only during an active live stream.
    /// Returns <c>false</c> when the page is not open or the badge is gone.
    /// </summary>
    Task<bool> IsCurrentStreamLiveAsync();
}
