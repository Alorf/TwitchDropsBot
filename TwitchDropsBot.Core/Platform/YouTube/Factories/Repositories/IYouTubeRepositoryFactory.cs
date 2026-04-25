using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.YouTube.Repository;

namespace TwitchDropsBot.Core.Platform.YouTube.Factories.Repositories;

public interface IYouTubeRepositoryFactory
{
    YouTubeHttpRepository Create(YouTubeUser user, ILogger logger);
}
