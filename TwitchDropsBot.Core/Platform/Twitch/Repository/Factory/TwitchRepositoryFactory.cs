using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;

namespace TwitchDropsBot.Core.Platform.Twitch.Repository.Factory;

public class TwitchRepositoryFactory : ITwitchRepositoryFactory
{
    private readonly IOptionsMonitor<BotSettings> _botSettings;

    public TwitchRepositoryFactory(IOptionsMonitor<BotSettings> botSettings)
    {
        _botSettings = botSettings;
    }

    public TwitchGqlRepository Create(TwitchUser user, ILogger logger)
    {
        return new TwitchGqlRepository(user, logger, _botSettings);
    }
}