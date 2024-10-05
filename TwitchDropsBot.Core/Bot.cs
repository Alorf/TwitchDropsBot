using System.Diagnostics;
using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;

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
        twitchUser.OnlyConnectedAccounts = appConfig.OnlyConnectedAccounts;

        twitchUser.Status = BotStatus.Seeking;

        // Get drops campaign
        List<DropCampaign> dropCampaigns = await twitchUser.GqlRequest.FetchDropsAsync();
        // Get new drops campaign system
        List<RewardCampaignsAvailableToUser> rewardCampaignsAvailableToUser = await twitchUser.GqlRequest.FetchRewardCampaignsAvailableToUserAsync();
        // Get inventory
        Inventory? inventory = await twitchUser.GqlRequest.FetchInventoryDropsAsync();

        List<AbstractCampaign> thingsToWatch = new List<AbstractCampaign>();

        await CheckCancellation();
        await CheckForClaim(inventory);

        await CheckCancellation();

        if (twitchUser.OnlyConnectedAccounts)
        {
            // 1. Filter out the drop campaigns that are connected to the account
            dropCampaigns =
                dropCampaigns
                    .Where(x => x.Self.IsAccountConnected)
                    .ToList();
        }

        thingsToWatch.AddRange(dropCampaigns);
        thingsToWatch.AddRange(rewardCampaignsAvailableToUser);

        if (twitchUser.OnlyFavouriteGames)
        {
            thingsToWatch = thingsToWatch.Where(x => x.Game.IsFavorite).ToList();
        }

        // Remove games from favourite list that are not in dropCampaigns
        twitchUser.FavouriteGames = twitchUser.FavouriteGames
            .Where(x => thingsToWatch.Any(y => y.Game.DisplayName == x))
            .ToList();

        var FavoutiteDropCampaigns = thingsToWatch
            .Where(x => twitchUser.FavouriteGames.Contains(x.Game.DisplayName))
            .ToList();

        FavoutiteDropCampaigns = FavoutiteDropCampaigns
            .OrderBy(x => twitchUser.FavouriteGames.IndexOf(x.Game.DisplayName))
            .ToList();

        // Remove from dropCampaigns FavoutiteDropCampaigns
        thingsToWatch = thingsToWatch
            .Where(x => !twitchUser.FavouriteGames.Contains(x.Game.DisplayName))
            .ToList();

        // Put favouriteDropCampaigns at the beginning of the list
        thingsToWatch.InsertRange(0, FavoutiteDropCampaigns);

        // Order dropCampaigns by inventory.dropCampaignsInProgress
        await CheckCancellation();
        thingsToWatch = thingsToWatch
            .OrderBy(x =>
                twitchUser.FavouriteGames.IndexOf(x.Game.DisplayName) != -1
                    ? twitchUser.FavouriteGames.IndexOf(x.Game.DisplayName)
                    : int.MaxValue)
            .ToList();

        bool timeBasedDropFound = false;
        AbstractCampaign? campaign;
        AbstractBroadcaster? broadcaster;
        TimeBasedDrop currentTimeBasedDrop;
        DropCurrentSession? dropCurrentSession = null; //New drops system also use that

        do
        {
            if (thingsToWatch.Count == 0)
            {
                throw new NoBroadcasterOrNoCampaignLeft();
            }

            await CheckCancellation();
            var result = await SelectBroadcasterAsync(thingsToWatch);
            campaign = result.campaign;
            broadcaster = result.broadcaster;

            if (campaign == null || broadcaster == null)
            {
                if (broadcaster == null)
                {
                    twitchUser.Logger.Log($"No broadcaster found for this campaign ({campaign?.Name}).");
                }

                var ToDelete = thingsToWatch.Find(x => x.Id == campaign.Id);
                thingsToWatch.Remove(ToDelete);
            }
            else
            {
                twitchUser.Logger.Log(
                    $"Current drop campaign: {campaign?.Name} ({campaign?.Game.DisplayName}), watching {broadcaster.Login} | {broadcaster.Id}");
                twitchUser.CurrentCampaign = campaign;

                dropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

                if (string.IsNullOrEmpty(dropCurrentSession?.DropId) || dropCurrentSession.CurrentMinutesWatched == dropCurrentSession.requiredMinutesWatched)
                {
                    await twitchUser.WatchStreamAsync(broadcaster.Login);
                    twitchUser.StreamURL = null;
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    dropCurrentSession =
                        await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
                }

                if (campaign is DropCampaign dropCampaign)
                {
                    currentTimeBasedDrop = dropCampaign!.TimeBasedDrops.Find((x) => x.Id == dropCurrentSession.DropId);
                    twitchUser.CurrentTimeBasedDrop = currentTimeBasedDrop;

                    // idk why but sometimes CurrentMinutesWatched is > requiredMinutesWatched for some reason
                    if (currentTimeBasedDrop == null || dropCurrentSession?.CurrentMinutesWatched >= dropCurrentSession?.requiredMinutesWatched)
                    {
                        var toDelete = thingsToWatch.Find(x => x.Id == dropCampaign.Id);
                        thingsToWatch.Remove(toDelete);
                    }
                    else
                    {
                        twitchUser.Logger.Log($"Time based drops : {currentTimeBasedDrop?.Name}");


                        timeBasedDropFound = true;
                    }
                }
                else
                {
                    if (dropCurrentSession?.CurrentMinutesWatched >= dropCurrentSession?.requiredMinutesWatched)
                    {
                        var toDelete = thingsToWatch.Find(x => x.Id == campaign.Id);
                        thingsToWatch.Remove(toDelete);
                    }
                    else
                    {
                        twitchUser.Logger.Log($"Time based drops : {campaign?.Name}");

                        timeBasedDropFound = true;
                    }
                }
            } 

            await Task.Delay(TimeSpan.FromSeconds(2));
        } while (!timeBasedDropFound);

        twitchUser.Status = BotStatus.Watching;
        await WatchStreamAsync(broadcaster, dropCurrentSession);
    }

    public async Task WatchStreamAsync(AbstractBroadcaster broadcaster, DropCurrentSession dropCurrentSession,
        int? minutes = null)
    {
        int stuckCounter = 0;
        int previousMinuteWatched = 0;
        var minuteWatched = dropCurrentSession.CurrentMinutesWatched;
        twitchUser.CurrendDropCurrentSession = dropCurrentSession;

        while (minuteWatched <
               (minutes ?? dropCurrentSession.requiredMinutesWatched) || dropCurrentSession.requiredMinutesWatched == 0) // While all the drops are not claimed
        {
            await CheckCancellation();

            try
            {
                await twitchUser.WatchStreamAsync(broadcaster.Login); // If not live, it will throw a 404 error    
            }
            catch (System.Exception ex)
            {
                twitchUser.Logger.Error(ex);
                twitchUser.StreamURL = null;
                throw new StreamOffline();
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

            if (previousMinuteWatched == minuteWatched)
            {
                stuckCounter++;
            }
            else
            {
                stuckCounter = 0;
            }

            if (stuckCounter >= 30)
            {
                twitchUser.StreamURL = null;
                await twitchUser.WatchStreamAsync(broadcaster.Login);
                await Task.Delay(TimeSpan.FromSeconds(20));
                dropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
            }

            previousMinuteWatched = minuteWatched;

            if (dropCurrentSession.requiredMinutesWatched == 0)
            {
                break;
            }

            twitchUser.Logger.Log(
                $"Waiting 20 seconds... {minuteWatched}/{dropCurrentSession.requiredMinutesWatched} minutes watched.");

            await Task.Delay(TimeSpan.FromSeconds(20));
        }
    }

    public async Task<(AbstractCampaign? campaign, AbstractBroadcaster? broadcaster)> SelectBroadcasterAsync(
        List<AbstractCampaign> campaigns)
    {
        AbstractBroadcaster broadcaster = null;
        foreach (var campaign in campaigns)
        {
            await CheckCancellation();
            twitchUser.Logger.Log($"Checking {campaign.Game.DisplayName}...");

            if (campaign.Game == null)
            {
                continue;
            }

            if (campaign is DropCampaign dropCampaign)
            {
                List<Channel>? channels = dropCampaign.GetChannels();

                if (channels != null && channels.Count >= 10)
                {
                    foreach (var channel in channels)
                    {
                        await CheckCancellation();
                        var tempBroadcaster = await twitchUser.GqlRequest.FetchStreamInformationAsync(channel.Name);

                        if (tempBroadcaster == null)
                        {
                            continue;
                        }

                        if (tempBroadcaster.IsLive() && tempBroadcaster.BroadcastSettings.Game?.Id != null &&
                            tempBroadcaster.BroadcastSettings.Game.Id == campaign.Game.Id)
                        {
                            broadcaster = tempBroadcaster;
                            twitchUser.CurrentBroadcaster = broadcaster;
                            break;
                        }
                    }

                    return (campaign, broadcaster);
                }
            }

            // Search for channel that potentially have the drops
            Game game = await twitchUser.GqlRequest.FetchDirectoryPageGameAsync(campaign.Game.Slug);

            // Select the channel that have the most viewers
            game.Streams.Edges = game.Streams.Edges.OrderByDescending(x => x.Node.ViewersCount).ToList();
            var edge = game.Streams.Edges.FirstOrDefault();
            if (edge != null)
            {
                broadcaster = edge.Node.Broadcaster;
                twitchUser.CurrentBroadcaster = broadcaster;
            }

            return (campaign, broadcaster);

        }

        return (null, null);
    }

    private async Task CheckForClaim(Inventory? inventory)
    {
        if (inventory?.DropCampaignsInProgress != null)
        {
            bool haveClaimed = false;
            // For every timebased drop, check if it is claimed
            foreach (var dropCampaignInProgress in inventory.DropCampaignsInProgress)
            {
                await CheckCancellation();
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
                await Task.Delay(TimeSpan.FromSeconds(20));
            }
        }
    }

    private async Task CheckCancellation()
    {
        if (twitchUser.CancellationTokenSource != null && twitchUser.CancellationTokenSource.Token.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
    }
}