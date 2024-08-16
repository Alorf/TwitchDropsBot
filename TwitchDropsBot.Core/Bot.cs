using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;

namespace TwitchDropsBot.Core;

public class Bot
{
    private TwitchUser twitchUser;

    public Bot(TwitchUser twitchUser)
    {
        this.twitchUser = twitchUser;
    }

    public async Task StartAsync()
    {
        AppConfig appConfig = AppConfig.GetConfig();
        twitchUser.FavouriteGames = appConfig.FavouriteGames;
        twitchUser.OnlyFavouriteGames = appConfig.OnlyFavouriteGames;

        // Get drops campaign
        List<DropCampaign> dropCampaigns = await twitchUser.GqlRequest.FetchDropsAsync();
        // Get inventory
        Inventory? inventory = await twitchUser.GqlRequest.FetchInventoryDropsAsync();
        
        if (inventory?.DropCampaignsInProgress != null)
        {
            bool haveClaimed = false;
            // For every timebased drop, check if it is claimed
            foreach (var dropCampaignInProgress in inventory.DropCampaignsInProgress)
            {
                foreach (var timeBasedDrop in dropCampaignInProgress.TimeBasedDrops)
                {
                    if (timeBasedDrop.Self.IsClaimed == false && timeBasedDrop.Self?.DropInstanceID != null)
                    {
                        await twitchUser.GqlRequest.ClaimDropAsync(timeBasedDrop.Self.DropInstanceID);
                        haveClaimed = true;
                    }
                }
            }

            if (haveClaimed)
            {
                await Task.Delay(20 * 1000);
            }
        }

        // 1. Filter out the drop campaigns that are connected to the account
        dropCampaigns =
            dropCampaigns
                .Where(x => x.Self.IsAccountConnected)
                .ToList();

        // if appConfig only favourite on true
        if (twitchUser.OnlyFavouriteGames)
        {
            dropCampaigns = dropCampaigns.Where(x => x.Game.IsFavorite).ToList();
        }

        if (inventory?.DropCampaignsInProgress != null)
        {
            // 2. remove timeBasedDrops from dropCampaigns where inventory.dropCampaignsInProgress game has isClaimed to true
            // Iterate over each DropCampaign
            foreach (var dropCampaign in dropCampaigns)
            {
                dropCampaign.TimeBasedDrops.RemoveAll((x) =>
                    inventory.DropCampaignsInProgress.FirstOrDefault(y => y.Id == dropCampaign.Id)?.TimeBasedDrops
                        .First(t => t.Id == x.Id).Self.IsClaimed ?? false);
            }
        }

        dropCampaigns.RemoveAll((x) => x.TimeBasedDrops.Count == 0);

        // Order dropCampaigns by inventory.dropCampaignsInProgress
        dropCampaigns = dropCampaigns
            .OrderBy(x =>
                twitchUser.FavouriteGames.IndexOf(x.Game.DisplayName) != -1
                    ? twitchUser.FavouriteGames.IndexOf(x.Game.DisplayName)
                    : int.MaxValue)
            .ToList();

        bool timeBasedDropFound = false;
        DropCampaign? campaign;
        AbstractBroadcaster? broadcaster;
        TimeBasedDrop currentTimeBasedDrop;
        DropCurrentSession? dropCurrentSession;

        twitchUser.Status = BotStatus.Seeking;

        do
        {
            var result = await SelectBroadcasterAsync(dropCampaigns);
            campaign = result.dropCampaign;
            broadcaster = result.broadcaster;

            if (campaign == null || broadcaster == null)
            {
                throw new NoBroadcasterOrNoCampaignFound();
            }

            twitchUser.Logger.Log(
                $"Current drop campaign: {campaign?.Name} ({campaign?.Game.DisplayName}), watching {broadcaster.Login} | {broadcaster.Id}");
            twitchUser.CurrentDropCampaign = campaign;

            dropCurrentSession =
                await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
            
            if (string.IsNullOrEmpty(dropCurrentSession?.DropId))
            {
                await twitchUser.WatchStreamAsync(broadcaster.Login);
                twitchUser.StreamURL = null;
                await Task.Delay(20 * 1000);

                dropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
            }

            currentTimeBasedDrop = campaign!.TimeBasedDrops.Find((x) => x.Id == dropCurrentSession.DropId);
            twitchUser.CurrentTimeBasedDrop = currentTimeBasedDrop;

            if (currentTimeBasedDrop == null)
            {
                var toDelete = dropCampaigns.Find(x => x.Id == campaign.Id);
                dropCampaigns.Remove(toDelete);
            }
            else
            {
                twitchUser.Logger.Log($"time based drops : {currentTimeBasedDrop?.Name}");

                timeBasedDropFound = true;
            }

            await Task.Delay(2000);
        } while (!timeBasedDropFound);

        await WatchStreamAsync(broadcaster, dropCurrentSession);
        twitchUser.StreamURL = null;
    }

    public async Task WatchStreamAsync(AbstractBroadcaster broadcaster, DropCurrentSession dropCurrentSession,
        int? minutes = null)
    {
        twitchUser.Status = BotStatus.Watching;

        var minuteWatched = dropCurrentSession.CurrentMinutesWatched;
        twitchUser.CurrendDropCurrentSession = dropCurrentSession;

        while (minuteWatched <
               (minutes ?? dropCurrentSession.requiredMinutesWatched) || dropCurrentSession.requiredMinutesWatched == 0) // While all the drops are not claimed
        {
            
            try
            {
                await twitchUser.WatchStreamAsync(broadcaster.Login); // If not live, it will throw a 404 error    
            }
            catch (System.Exception ex)
            {
                twitchUser.Logger.Error(ex);
                twitchUser.StreamURL = null;
                throw new System.Exception("Stream is no longer live");
            }

            try
            {
                dropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

                twitchUser.CurrendDropCurrentSession = dropCurrentSession;
            }
            catch (System.Exception e)
            {
                twitchUser.Logger.Error(e);
            }

            minuteWatched = dropCurrentSession.CurrentMinutesWatched;

            if (dropCurrentSession.requiredMinutesWatched == 0)
            {
                break;
            }
            
            twitchUser.Logger.Log(
                $"Waiting 20 seconds... {minuteWatched}/{dropCurrentSession.requiredMinutesWatched} minutes watched.");
            
            await Task.Delay(20 * 1000);
        }
    }

    public async Task<(DropCampaign? dropCampaign, AbstractBroadcaster? broadcaster)> SelectBroadcasterAsync(
        List<DropCampaign> dropCampaigns)
    {
        AbstractBroadcaster broadcaster = null;
        foreach (var dropcampaign in dropCampaigns)
        {
            twitchUser.Logger.Log($"Checking {dropcampaign.Game.DisplayName}...");

            if (dropcampaign.Game == null)
            {
                continue;
            }

            List<Channel>? channels = dropcampaign.GetChannels();

            if (channels != null)
            {
                foreach (var channel in channels)
                {
                    var tempBroadcaster = await twitchUser.GqlRequest.FetchStreamInformationAsync(channel.Name);

                    if (tempBroadcaster == null)
                    {
                        continue;
                    }

                    if (tempBroadcaster.IsLive() && tempBroadcaster.BroadcastSettings.Game.Id != null &&
                        tempBroadcaster.BroadcastSettings.Game.Id == dropcampaign.Game.Id)
                    {
                        broadcaster = tempBroadcaster;
                        twitchUser.CurrentBroadcaster = broadcaster;
                        return (dropcampaign, broadcaster);
                    }
                }
            }
            else
            {
                // Search for channel that potentially have the drops
                Game game = await twitchUser.GqlRequest.FetchDirectoryPageGameAsync(dropcampaign.Game.Slug);

                // Select the channel that have the most viewers
                game.Streams.Edges = game.Streams.Edges.OrderByDescending(x => x.Node.ViewersCount).ToList();
                var edge = game.Streams.Edges.FirstOrDefault();
                if (edge != null)
                {
                    broadcaster = edge.Node.Broadcaster;
                    twitchUser.CurrentBroadcaster = broadcaster;
                }

                return (dropcampaign, broadcaster);
            }
        }

        return (null, null);
    }
}