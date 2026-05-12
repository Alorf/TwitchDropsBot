using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Youtube.Bot;
using TwitchDropsBot.Core.Platform.Youtube.Repository;

namespace TwitchDropsBot.Core.Platform.Youtube.Factories.Repositories;

public interface IYoutubeRepositoryFactory
{
    YoutubeHttpRepository Create(YoutubeUser user, ILogger logger);
}