using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.WatchManager;

namespace TwitchDropsBot.Core.Platform.Twitch.Factories.WatchManager;

public interface ITwitchWatchManagerFactory
{
    ITwitchWatchManager Create(TwitchUser user);

}