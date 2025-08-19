using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.Core.WatchManager;

public abstract class WatchManager
{
    protected TwitchUser twitchUser;
    protected CancellationTokenSource? cancellationTokenSource;
    public WatchManager(TwitchUser twitchUser, CancellationTokenSource cancellationTokenSource)
    {
        this.twitchUser = twitchUser;
        this.cancellationTokenSource = cancellationTokenSource;
    }

    public abstract Task WatchStreamAsync(AbstractBroadcaster broadcaster);
    public abstract void Close();
}