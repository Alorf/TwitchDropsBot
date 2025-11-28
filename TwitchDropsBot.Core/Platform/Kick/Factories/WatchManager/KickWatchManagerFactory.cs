using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Kick.Factories.WatchManager;

public class KickWatchManagerFactory : IKickWatchManagerFactory
{
    private readonly IOptionsMonitor<BotSettings> _settings;
    private readonly BrowserService _browserService;

    public KickWatchManagerFactory(
        IOptionsMonitor<BotSettings> settings,
        BrowserService browserService)
    {
        _settings = settings;
        _browserService = browserService;
    }

    public IKickWatchManager Create(KickUser user)
    {
        var type = _settings.CurrentValue.KickSettings.WatchManager;

        return type switch
        {
            "WatchRequest" => new WatchRequest(
                user.KickRepository, user.Logger),

            "WatchBrowser" => new WatchBrowser(
                user, user.Logger, _browserService),

            _ => new WatchRequest(
                user.KickRepository, user.Logger)
        };
    }
}