using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Youtube.Bot;

public class YoutubeBot : BaseBot<YoutubeUser>
{
    public YoutubeBot(YoutubeUser user, ILogger logger, NotificationService notificationService, IOptionsMonitor<BotSettings> botSettings) : base(user,
        logger, notificationService, botSettings)
    {
    }


    public override List<string> GetUserFavoriteGames()
    {
        throw new NotImplementedException();
    }

    protected override Task StartAsync()
    {
        throw new NotImplementedException();
    }
}