using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Twitch.Bot;

namespace TwitchDropsBot.Core.Platform.Twitch.Repository.Factory;

public interface ITwitchRepositoryFactory
{
    TwitchGqlRepository Create(TwitchUser user, ILogger logger);
}