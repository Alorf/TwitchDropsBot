using Discord;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;

namespace TwitchDropsBot.Core.Platform.Shared.Bots;

public class BotRunner
{
    public static Task StartBot<TUser>(BaseBot<TUser> bot) where TUser : BotUser
    {
        TimeSpan waitingTime = TimeSpan.FromSeconds(bot.BotSettings.waitingSeconds);

        return Task.Run(async () =>
        {
            await bot.StartBot();
        });
    }
}