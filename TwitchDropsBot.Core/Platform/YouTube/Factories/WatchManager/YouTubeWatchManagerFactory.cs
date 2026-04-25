using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.YouTube.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.YouTube.Factories.WatchManager;

public class YouTubeWatchManagerFactory : IYouTubeWatchManagerFactory
{
    private readonly BrowserService _browserService;

    public YouTubeWatchManagerFactory(BrowserService browserService)
    {
        _browserService = browserService;
    }

    public IYouTubeWatchManager Create(YouTubeUser user)
    {
        return new WatchBrowser(user, user.Logger, _browserService);
    }
}
