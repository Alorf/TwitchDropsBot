using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Youtube.Bot;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;

namespace TwitchDropsBot.Core.Platform.Youtube.WatchManager;

public class WatchBrowser : WatchBrowser<YoutubeUser, string, string>, IYoutubeWatchManager
{
    public WatchBrowser(YoutubeUser user, ILogger logger, BrowserService browserService) : base(user, logger, browserService)
    {
    }

    public override Task WatchStreamAsync(string broadcaster, string game)
    {
        throw new NotImplementedException();
    }
}