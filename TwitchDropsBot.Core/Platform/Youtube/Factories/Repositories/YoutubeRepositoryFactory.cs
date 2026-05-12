using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Youtube.Bot;
using TwitchDropsBot.Core.Platform.Youtube.Repository;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Youtube.Factories.Repositories;

public class YoutubeRepositoryFactory : IYoutubeRepositoryFactory
{
    private readonly IOptionsMonitor<BotSettings> _botSettings;

    public YoutubeRepositoryFactory(IOptionsMonitor<BotSettings> botSettings)
    {
        _botSettings = botSettings;
    }

    public YoutubeHttpRepository Create(YoutubeUser user, ILogger logger)
    {
        return new YoutubeHttpRepository(user, logger, _botSettings);
    }
}