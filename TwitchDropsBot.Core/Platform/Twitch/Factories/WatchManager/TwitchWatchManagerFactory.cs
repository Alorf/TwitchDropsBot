using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.WatchManager;

namespace TwitchDropsBot.Core.Platform.Twitch.Factories.WatchManager;

public class TwitchWatchManagerFactory : ITwitchWatchManagerFactory
{
    private readonly IOptionsMonitor<BotSettings> _settings;
    private readonly BrowserService _browserService;

    public TwitchWatchManagerFactory(
        IOptionsMonitor<BotSettings> settings,
        BrowserService browserService)
    {
        _settings = settings;
        _browserService = browserService;
    }

    public ITwitchWatchManager Create(TwitchUser user)
    {
        var type = _settings.CurrentValue.TwitchSettings.WatchManager;

        return type switch
        {
            "WatchRequest" => new WatchRequest(
                user, user.Logger, false),

            "WatchBrowser" => new WatchBrowser(
                user, user.Logger, _browserService),

            _ => new WatchRequest(
                user, user.Logger, false)
        };
    }
}