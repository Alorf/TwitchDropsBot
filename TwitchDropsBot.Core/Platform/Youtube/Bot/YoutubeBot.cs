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
    public YoutubeBot(YoutubeUser user, ILogger logger, NotificationService notificationService,
        IOptionsMonitor<BotSettings> botSettings) : base(user,
        logger, notificationService, botSettings)
    {
    }


    public override List<string> GetUserFavoriteGames()
    {
        return BotUser.FavouriteGames;
    }

    protected override async Task StartAsync()
    {
        var favouriteHandles = GetUserFavoriteGames();

        var liveChannel = await PickLiveChannel(favouriteHandles);

        if (liveChannel is null)
        {
            throw new NoBroadcasterOrNoCampaignLeft();
        }


        await WatchStreamAsync(liveChannel);
    }

    private async Task<string?> PickLiveChannel(List<string> handles)
    {
        bool isLive = false;
        foreach (var handle in handles)
        {
            isLive = await BotUser.YoutubeRepository.IsLive(handle);

            if (isLive)
            {
                return handle;
            }
        }

        return null;
    }

    private async Task WatchStreamAsync(string handle)
    {
        while (await BotUser.YoutubeRepository.IsLive(handle))
        {
            var streamUrl = $"https://www.youtube.com/@{handle}/live";
            await BotUser.WatchManager.WatchStreamAsync(streamUrl, handle);

            Logger.LogInformation("Finished watching stream for {Handle}, checking if still live...", handle);
            await Task.Delay(TimeSpan.FromSeconds(20));

        }
    }
}