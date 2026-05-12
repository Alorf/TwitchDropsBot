using TwitchDropsBot.Core.Platform.Youtube.Bot;
using TwitchDropsBot.Core.Platform.Youtube.WatchManager;

namespace TwitchDropsBot.Core.Platform.Youtube.Factories.WatchManager;

public interface IYoutubeWatchManagerFactory
{
    IYoutubeWatchManager Create(YoutubeUser user);

}