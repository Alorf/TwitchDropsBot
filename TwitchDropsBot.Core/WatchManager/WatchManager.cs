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
    public abstract Task<DropCurrentSession?> FakeWatchAsync(AbstractBroadcaster broadcaster, int tryCount = 0);
    public abstract void Close();
    
    protected void CheckCancellation()
    {
        if (cancellationTokenSource != null &&
            cancellationTokenSource.Token.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
    }
    
}