using TwitchDropsBot.Core.Platform.Shared.Bots;

namespace TwitchDropsBot.Core.Platform.Shared.WatchManager;

public interface IWatchManager<TUser, TGame, TBroadcaster> where TUser : BotUser
{
    TUser BotUser { get; }
    
    Task WatchStreamAsync(TBroadcaster broadcaster, TGame game);
    void Close();
}