using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;

namespace TwitchDropsBot.Core.Platform.Shared.Factories.Bot;

public class BotFactory
{

    private readonly NotificationService _notificationService;
    private readonly IOptionsMonitor<BotSettings> _botSettings;
    
    public BotFactory(NotificationService notificationService, IOptionsMonitor<BotSettings> botSettings)
    {
        _notificationService = notificationService;
        _botSettings = botSettings;
    }

    public TwitchBot CreateTwitchBot(TwitchUser user, ILogger logger)
    {
        return new TwitchBot(user, logger, _notificationService, _botSettings); 
    }

    public KickBot CreateKickBot(KickUser user, ILogger logger)
    {
        return new KickBot(user, logger, _notificationService, _botSettings); 
    }
}