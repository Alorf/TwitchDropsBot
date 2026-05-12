using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Youtube.Bot;
using TwitchDropsBot.Core.Platform.Youtube.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Youtube.Factories.WatchManager;

public class YoutubeWatchManagerFactory : IYoutubeWatchManagerFactory
{
    private readonly IOptionsMonitor<BotSettings> _settings;
    private readonly BrowserService _browserService;

    public YoutubeWatchManagerFactory(
        IOptionsMonitor<BotSettings> settings,
        BrowserService browserService)
    {
        _settings = settings;
        _browserService = browserService;
    }

    public IYoutubeWatchManager Create(YoutubeUser user)
    {
        var type = _settings.CurrentValue.YoutubeSettings.WatchManager;

        return type switch
        {
            "WatchBrowser" => new WatchBrowser(
                user, user.Logger, _browserService),

            _ => new WatchBrowser(
                user, user.Logger, _browserService),
        };
    }
}