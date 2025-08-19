using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.Core;

public class Bot
{
    private TwitchUser twitchUser;
    private AppConfig config;
    private static readonly object _configLock = new();

    public Bot(TwitchUser twitchUser)
    {
        this.twitchUser = twitchUser;
        config = AppConfig.Instance;
    }

    public static Task StartBot(TwitchUser twitchUser)
    {
        var bot = new Bot(twitchUser);
        TimeSpan waitingTime;
        twitchUser.CancellationTokenSource = new CancellationTokenSource();
        
        return Task.Run(async () =>
        {
            var config = AppConfig.Instance;

            while (true)
            {
                try
                {
                    await bot.StartAsync();
                    waitingTime = TimeSpan.FromSeconds(20);
                }
                catch (NoBroadcasterOrNoCampaignLeft ex)
                {
                    twitchUser.Logger.Info(ex.Message);
                    twitchUser.Logger.Info($"Waiting {config.waitingSeconds} seconds before trying again.");
                    waitingTime = TimeSpan.FromSeconds(config.waitingSeconds);
                }
                catch (StreamOffline ex)
                {
                    twitchUser.Logger.Info(ex.Message);
                    twitchUser.Logger.Info($"Waiting {config.waitingSeconds} seconds before trying again.");
                    waitingTime = TimeSpan.FromSeconds(config.waitingSeconds);
                }
                catch (OperationCanceledException ex)
                {
                    twitchUser.Logger.Info(ex.Message);
                    twitchUser.CancellationTokenSource = new CancellationTokenSource();
                    waitingTime = TimeSpan.FromSeconds(10);
                }
                catch (System.Exception ex)
                {
                    twitchUser.Logger.Error(ex);
                    waitingTime = TimeSpan.FromSeconds(config.waitingSeconds);
                }

                twitchUser.WatchManager.Close();
                twitchUser.Status = BotStatus.Idle;

                await Task.Delay(waitingTime);
            }
        });
    }

    private async Task StartAsync()
    {
        lock (_configLock)
        {
            config.GetConfig();
        }
        
        twitchUser.FavouriteGames = twitchUser.PersonalFavouriteGames.Count > 0 ? twitchUser.PersonalFavouriteGames : config.FavouriteGames;
        twitchUser.OnlyFavouriteGames = config.OnlyFavouriteGames;
        twitchUser.OnlyConnectedAccounts = config.OnlyConnectedAccounts;

        // Get campaigns
        var thingsToWatch = await twitchUser.GqlRequest.FetchDropsAsync();
        var inventory = await twitchUser.GqlRequest.FetchInventoryDropsAsync();

        twitchUser.Inventory = inventory;
        twitchUser.Status = BotStatus.Seeking;

        CheckCancellation();
        await CheckForClaim(inventory);
        CheckCancellation();

        if (twitchUser.OnlyConnectedAccounts)
        {
            thingsToWatch.RemoveAll(x =>
                x is DropCampaign dropCampaign && !dropCampaign.Self.IsAccountConnected &&
                dropCampaign.AccountLinkURL != "https://twitch.tv/");
        }

        if (twitchUser.OnlyFavouriteGames)
        {
            thingsToWatch.RemoveAll(x => !x.Game?.IsFavorite ?? false);
        }

        // Assuming you have a list of favorite game names
        var favoriteGameNames = twitchUser.FavouriteGames;

        // Order things to watch by the order of favorite game names and drop that is ending soon
        CheckCancellation();
        thingsToWatch = thingsToWatch
            .Where(x => x.Game is not null)
            .OrderBy(x =>
                favoriteGameNames.IndexOf(x.Game!.DisplayName) == -1
                    ? int.MaxValue
                    : favoriteGameNames.IndexOf(x.Game.DisplayName))
            .ThenBy(x => (x as DropCampaign)?.EndAt ?? DateTime.MaxValue)
            .ToList();

        var timeBasedDropFound = false;
        AbstractCampaign? campaign;
        AbstractBroadcaster? broadcaster;
        TimeBasedDrop currentTimeBasedDrop;
        DropCurrentSession? dropCurrentSession = null; // Rewards also use that

        do
        {
            if (thingsToWatch.Count == 0)
            {
                throw new NoBroadcasterOrNoCampaignLeft();
            }

            CheckCancellation();
            var result = await SelectBroadcasterAsync(thingsToWatch);
            campaign = result.campaign;
            broadcaster = result.broadcaster;

            if (campaign is null)
            {
                twitchUser.Logger.Log("No campaign found.");
                continue;
            }

            if (broadcaster is null)
            {
                twitchUser.Logger.Log($"No broadcaster found for this campaign ({campaign.Name}).");
                thingsToWatch.RemoveAll(x => x.Id == campaign.Id);

                continue;
            }
            
            twitchUser.Logger.Log(
                $"Current drop campaign: {campaign.Name} ({campaign.Game?.DisplayName}), watching {broadcaster.Login} | {broadcaster.Id}");
            twitchUser.CurrentCampaign = campaign;

            dropCurrentSession =
                await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

            // fixme: if there is two campaigns for the same game, twitch will chose one of them, we cannot decide which one except if there is a special channel to wach

            if (string.IsNullOrEmpty(dropCurrentSession?.DropId) || dropCurrentSession.Channel.Id != broadcaster.Id ||
                dropCurrentSession.CurrentMinutesWatched == dropCurrentSession.requiredMinutesWatched)
            {
                if (dropCurrentSession != null && dropCurrentSession?.Channel != null &&
                    dropCurrentSession?.Channel.Id != broadcaster.Id)
                {
                    twitchUser.Logger.Log(
                        $"Drop found but not the right channel, watching 20 sec to init the drop (got {dropCurrentSession.Channel.Name} instead of {broadcaster.Login})");
                }

                // Sometimes, we have to watch 2 or 3 times to init the drop or it will skip
                for (var i = 0; i < config.AttemptToWatch; i++)
                {
                    twitchUser.Logger.Log("No time based drop found, watching 20 sec to init the drop");
                    dropCurrentSession = await FakeWatchAsync(broadcaster);
                }
            }

            if (campaign is DropCampaign dropCampaign)
            {
                currentTimeBasedDrop = dropCampaign.TimeBasedDrops.Find(x => x.Id == dropCurrentSession.DropId);
                twitchUser.CurrentTimeBasedDrop = currentTimeBasedDrop;

                // idk why but sometimes CurrentMinutesWatched is > requiredMinutesWatched for some reason
                if (currentTimeBasedDrop == null || dropCurrentSession?.CurrentMinutesWatched >=
                    dropCurrentSession?.requiredMinutesWatched)
                {
                    twitchUser.Logger.Log("Time based drop not found, skipping");
                    thingsToWatch.RemoveAll(x => x.Id == dropCampaign.Id);
                }
                else
                {
                    twitchUser.Logger.Log($"Time based drops : {currentTimeBasedDrop.Name}");

                    timeBasedDropFound = true;
                }
            }
            else if (campaign is RewardCampaignsAvailableToUser rewardCampaign)
            {
                var rewardDropCampaign = await twitchUser.GqlRequest.FetchTimeBasedDropsAsync(campaign.Id);

                if (rewardDropCampaign is null)
                {
                    twitchUser.Logger.Log($"{campaign.Name} has no time based drops.");

                }
                else
                {
                    rewardCampaign.DistributionType = rewardDropCampaign.TimeBasedDrops[0].GetDistributionType();
                }

                if (dropCurrentSession?.CurrentMinutesWatched >= dropCurrentSession?.requiredMinutesWatched)
                {
                    thingsToWatch.RemoveAll(x => x.Id == campaign.Id);
                }
                else
                {
                    twitchUser.Logger.Log($"Time based drops : {campaign.Name}");
                    twitchUser.Logger.Log($"Distribution type : {rewardCampaign.DistributionType}");
                    timeBasedDropFound = true;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        } while (!timeBasedDropFound);

        if (broadcaster is null)
        {
            twitchUser.Logger.Log("Time based drops found but not broadcaster");
            return;
        }
        
        if (dropCurrentSession is null)
        {
            twitchUser.Logger.Log("Time based drops found but not current drop session");
            return;
        }

        twitchUser.Status = BotStatus.Watching;
        await WatchStreamAsync(broadcaster, dropCurrentSession);
        await campaign?.NotifiateAsync(twitchUser);
    }

    private async Task WatchStreamAsync(AbstractBroadcaster broadcaster, DropCurrentSession dropCurrentSession,
        int? minutes = null)
    {
        var stuckCounter = 0;
        var previousMinuteWatched = 0;
        var minuteWatched = dropCurrentSession.CurrentMinutesWatched;
        twitchUser.CurrentDropCurrentSession = dropCurrentSession;

        while (minuteWatched <
               (minutes ?? dropCurrentSession.requiredMinutesWatched) ||
               dropCurrentSession.requiredMinutesWatched == 0) // While all the drops are not claimed
        {
            CheckCancellation();

            try
            {
                await twitchUser.WatchManager
                    .WatchStreamAsync(broadcaster); // If not live, it will throw a 404 error    
            }
            catch (System.Exception ex)
            {
                twitchUser.Logger.Error(ex);
                twitchUser.WatchManager.Close();
                throw new StreamOffline();
            }

            try
            {
                var newDropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

                if (newDropCurrentSession is null)
                {
                    twitchUser.Logger.Log("Can't fetch new current drop session");
                }
                else
                {
                    dropCurrentSession = newDropCurrentSession;
                }

                twitchUser.CurrentDropCurrentSession = dropCurrentSession;
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
                twitchUser.WatchManager.Close();
                await twitchUser.WatchManager.WatchStreamAsync(broadcaster);
                await Task.Delay(TimeSpan.FromSeconds(20));
                
                var newDropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

                if (newDropCurrentSession is null)
                {
                    twitchUser.Logger.Log("Can't fetch new current drop session after being stuck");
                }
                else
                {
                    dropCurrentSession = newDropCurrentSession;
                }
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
        twitchUser.WatchManager.Close();
    }

    private async Task<(AbstractCampaign? campaign, AbstractBroadcaster? broadcaster)> SelectBroadcasterAsync(
        List<AbstractCampaign> campaigns)
    {
        AbstractBroadcaster? broadcaster = null;

        if (config.AvoidCampaign.Count > 0)
        {
            campaigns.RemoveAll(x => config.AvoidCampaign.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
            campaigns.RemoveAll(x => config.AvoidCampaign.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
        }

        foreach (var campaign in campaigns)
        {
            if (campaign.Game is null)
            {
                twitchUser.Logger.Log($"Skipping campaign {campaign.Name} because game is null.");
                continue;
            }
            
            CheckCancellation();
            twitchUser.Logger.Log($"Checking {campaign.Game.DisplayName}...");

            if (campaign is DropCampaign dropCampaign)
            {
                var tempDropCampaign = await twitchUser.GqlRequest.FetchTimeBasedDropsAsync(campaign.Id);
                dropCampaign.TimeBasedDrops = tempDropCampaign.TimeBasedDrops;
                dropCampaign.Game = tempDropCampaign.Game;
                dropCampaign.Allow = tempDropCampaign.Allow;

                var channels = dropCampaign.GetChannels();

                if (config.WatchSpecificStreamer.Count > 0 && channels is not null)
                {
                    channels = channels.Where(x =>
                        config.WatchSpecificStreamer.Contains(x.Name, StringComparer.OrdinalIgnoreCase)).ToList();
                }

                if (channels != null && channels.Count <= 10)
                {
                    foreach (var channel in channels)
                    {
                        CheckCancellation();
                        var tempBroadcaster = await twitchUser.GqlRequest.FetchStreamInformationAsync(channel.Name);

                        if (tempBroadcaster is null) continue;

                        if (tempBroadcaster.IsLive() && tempBroadcaster.BroadcastSettings.Game?.Id != null &&
                            (campaign.Game.DisplayName == "Special Events" ||
                             tempBroadcaster.BroadcastSettings.Game.Id == campaign.Game.Id))
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
            var game = await twitchUser.GqlRequest.FetchDirectoryPageGameAsync(campaign.Game.Slug,
                campaign is DropCampaign);

            if (game is null)
            {
                twitchUser.Logger.Log($"No game found for slug {campaign.Game.Slug}.");
                continue;
            }

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
            var haveClaimed = false;
            // For every timebased drop, check if it is claimed
            foreach (var dropCampaignInProgress in inventory.DropCampaignsInProgress)
            {
                CheckCancellation();
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

    private async Task<DropCurrentSession?> FakeWatchAsync(AbstractBroadcaster broadcaster)
    {
        // Trying to init the drop

        await twitchUser.WatchManager.WatchStreamAsync(broadcaster);
        await Task.Delay(TimeSpan.FromSeconds(20));
        twitchUser.WatchManager.Close();

        return await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
    }

    private void CheckCancellation()
    {
        if (twitchUser.CancellationTokenSource != null &&
            twitchUser.CancellationTokenSource.Token.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
    }
}