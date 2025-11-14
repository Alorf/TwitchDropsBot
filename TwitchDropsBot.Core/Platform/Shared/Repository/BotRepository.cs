using TwitchDropsBot.Core.Platform.Shared.Bots;

namespace TwitchDropsBot.Core.Platform.Shared.Repository;

public abstract class BotRepository<TUser> where TUser : BotUser
{
    protected TUser BotUser;
}