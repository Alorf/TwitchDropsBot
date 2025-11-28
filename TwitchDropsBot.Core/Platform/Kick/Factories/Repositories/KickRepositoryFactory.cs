using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Kick.Factories.Repositories;

public class KickRepositoryFactory : IKickRepositoryFactory
{
    private readonly IOptionsMonitor<BotSettings> _botSettings;

    public KickRepositoryFactory(IOptionsMonitor<BotSettings> botSettings)
    {
        _botSettings = botSettings;
    }

    public KickHttpRepository Create(KickUser user, ILogger logger)
    {
        return new KickHttpRepository(user, logger, _botSettings);
    }
}