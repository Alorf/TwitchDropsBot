using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Repository;

namespace TwitchDropsBot.Core.Platform.Kick.Factories.Repositories;

public interface IKickRepositoryFactory
{
    KickHttpRepository Create(KickUser user, ILogger logger);
}