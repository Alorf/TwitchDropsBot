using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Services;

namespace TwitchDropsBot.Core.Platform.Shared.WatchManager;

public abstract class WatchBrowser<TUser, TGame, TBroadcaster> : IWatchManager<TUser, TGame, TBroadcaster>, IAsyncDisposable
    where TUser : BotUser
{
    public TUser BotUser { get; }
    protected ILogger Logger;

    protected bool _disposed = false;
    protected IPage? Page;
    protected readonly BrowserService BrowserService;

    protected WatchBrowser(TUser botUser, ILogger baseLogger, BrowserService browserService)
    {
        BotUser = botUser;
        Logger = baseLogger;
        BrowserService = browserService;
    }

    public abstract Task WatchStreamAsync(TBroadcaster broadcaster, TGame game);

    public void Close()
    {
        _ = DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        if (Page != null)
        {
            await BrowserService.RemoveUserAsync(BotUser);
            Page = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WatchBrowser()
    {
        Close();
    }
}