using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.YouTube.WatchManager;

namespace TwitchDropsBot.Core.Platform.YouTube.Factories.WatchManager;

public interface IYouTubeWatchManagerFactory
{
    IYouTubeWatchManager Create(YouTubeUser user);
}
