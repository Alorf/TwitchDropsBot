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
}
