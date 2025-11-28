using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.WatchManager;

namespace TwitchDropsBot.Core.Platform.Kick.Factories.WatchManager;

public interface IKickWatchManagerFactory
{
    IKickWatchManager Create(KickUser user);

}