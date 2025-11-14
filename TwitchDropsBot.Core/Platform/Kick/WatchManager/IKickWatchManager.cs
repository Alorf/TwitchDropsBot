using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;

namespace TwitchDropsBot.Core.Platform.Kick.WatchManager;

public interface IKickWatchManager : IWatchManager<KickUser, Category, Channel>
{
    
}