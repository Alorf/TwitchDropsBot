using System.Diagnostics;
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
        twitchUser.OnlyConnectedAccounts = appConfig.OnlyConnectedAccounts;

        twitchUser.Status = BotStatus.Seeking;

        // Get drops campaign
        List<DropCampaign> dropCampaigns = await twitchUser.GqlRequest.FetchDropsAsync();
        // Get inventory
        Inventory? inventory = await twitchUser.GqlRequest.FetchInventoryDropsAsync();

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
        
        if (twitchUser.OnlyFavouriteGames)
        {
            dropCampaigns = dropCampaigns.Where(x => x.Game.IsFavorite).ToList();
        }
        
        // Remove games from favourite list that are not in dropCampaigns
        twitchUser.FavouriteGames = twitchUser.FavouriteGames
            .Where(x => dropCampaigns.Any(y => y.Game.DisplayName == x))
            .ToList();

        var FavoutiteDropCampaigns = dropCampaigns
            .Where(x => twitchUser.FavouriteGames.Contains(x.Game.DisplayName))
            .ToList();
        
        FavoutiteDropCampaigns = FavoutiteDropCampaigns
            .OrderBy(x => twitchUser.FavouriteGames.IndexOf(x.Game.DisplayName))
            .ToList();
        
        // Remove from dropCampaigns FavoutiteDropCampaigns
        dropCampaigns = dropCampaigns
            .Where(x => !twitchUser.FavouriteGames.Contains(x.Game.DisplayName))
            .ToList();
        
        // Put favouriteDropCampaigns at the beginning of the list
        dropCampaigns.InsertRange(0, FavoutiteDropCampaigns);

        // Order dropCampaigns by inventory.dropCampaignsInProgress
        await CheckCancellation();
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
        DropCurrentSession? dropCurrentSession = null;

        do
        {
            if (dropCampaigns.Count == 0)
            {
                throw new NoBroadcasterOrNoCampaignLeft();
            }
            
            await CheckCancellation();
            var result = await SelectBroadcasterAsync(dropCampaigns);
            campaign = result.dropCampaign;
            broadcaster = result.broadcaster;
            
            if (campaign == null || broadcaster == null)
            {
                if (broadcaster == null)
                {
                    twitchUser.Logger.Log($"No broadcaster found for this campaign ({campaign?.Name}).");
                }
                
                var ToDelete = dropCampaigns.Find(x => x.Id == campaign.Id);
                dropCampaigns.Remove(ToDelete);
            }
            else
            {
                twitchUser.Logger.Log(
                    $"Current drop campaign: {campaign?.Name} ({campaign?.Game.DisplayName}), watching {broadcaster.Login} | {broadcaster.Id}");
                twitchUser.CurrentDropCampaign = campaign;

                dropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

                if (string.IsNullOrEmpty(dropCurrentSession?.DropId))
                {
                    await twitchUser.WatchStreamAsync(broadcaster.Login);
                    twitchUser.StreamURL = null;
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    dropCurrentSession =
                        await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
                }

                currentTimeBasedDrop = campaign!.TimeBasedDrops.Find((x) => x.Id == dropCurrentSession.DropId);
                twitchUser.CurrentTimeBasedDrop = currentTimeBasedDrop;

                // idk why but sometimes CurrentMinutesWatched is > requiredMinutesWatched for some reason
                if (currentTimeBasedDrop == null || dropCurrentSession?.CurrentMinutesWatched >= dropCurrentSession?.requiredMinutesWatched)
                {
                    var toDelete = dropCampaigns.Find(x => x.Id == campaign.Id);
                    dropCampaigns.Remove(toDelete);
                }
                else
                {
                    twitchUser.Logger.Log($"Time based drops : {currentTimeBasedDrop?.Name}");


                    timeBasedDropFound = true;
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

    public async Task<(DropCampaign? dropCampaign, AbstractBroadcaster? broadcaster)> SelectBroadcasterAsync(
        List<DropCampaign> dropCampaigns)
    {
        AbstractBroadcaster broadcaster = null;
        foreach (var dropcampaign in dropCampaigns)
        {
            await CheckCancellation();
            twitchUser.Logger.Log($"Checking {dropcampaign.Game.DisplayName}...");

            if (dropcampaign.Game == null)
            {
                continue;
            }

            List<Channel>? channels = dropcampaign.GetChannels();

            if (channels != null || channels?.Count >= 10)
            {
                foreach (var channel in channels)
                {
                    await CheckCancellation();
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
                        break;
                    }
                }

                return (dropcampaign, broadcaster);
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