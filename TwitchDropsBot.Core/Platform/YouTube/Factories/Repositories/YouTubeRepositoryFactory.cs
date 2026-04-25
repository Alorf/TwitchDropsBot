using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.YouTube.Repository;

namespace TwitchDropsBot.Core.Platform.YouTube.Factories.Repositories;

public class YouTubeRepositoryFactory : IYouTubeRepositoryFactory
{
    public YouTubeHttpRepository Create(YouTubeUser user, ILogger logger)
    {
        return new YouTubeHttpRepository(user, logger);
    }
}
