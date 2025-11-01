using Discord;
using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.Core;

public class Bot
{
    private TwitchUser twitchUser;
    private AppConfig config;

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
                catch (CurrentDropSessionChanged ex)
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

                    if (!string.IsNullOrEmpty(config.WebhookURL))
                    {
                        await twitchUser.SendWebhookAsync(new List<Embed>
                        {
                            new EmbedBuilder()
                                .WithTitle($"ERROR : {twitchUser.Login} - {DateTime.Now}")
                                .WithDescription($"```\n{ex}\n```")
                                .WithColor(Discord.Color.Red)
                                //.WithUrl(action.Url)
                                .Build()
                        });
                    }

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
        twitchUser.FavouriteGames = twitchUser.PersonalFavouriteGames.Count > 0
            ? twitchUser.PersonalFavouriteGames
            : config.FavouriteGames;
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

        var linqToWatch = from thingToWatch in thingsToWatch
            where thingToWatch.Game is not null
            orderby
                favoriteGameNames.IndexOf(thingToWatch.Game!.DisplayName) == -1
                    ? int.MaxValue
                    : favoriteGameNames.IndexOf(thingToWatch.Game.DisplayName),
                (thingToWatch as DropCampaign)?.EndAt ?? DateTime.MaxValue
            select thingToWatch;

        thingsToWatch = linqToWatch.ToList();

        TimeBasedDrop? timeBasedDrop = null;
        DropCurrentSession? dropCurrentSession = null;
        AbstractBroadcaster? broadcaster = null;
        AbstractCampaign? campaign = null;

        do
        {
            if (thingsToWatch.Count == 0)
            {
                throw new NoBroadcasterOrNoCampaignLeft();
            }

            CheckCancellation();
            (campaign, broadcaster) = await SelectBroadcasterAsync(thingsToWatch, inventory);

            if (campaign is null)
            {
                twitchUser.Logger.Log("No campaign found.");
                if (thingsToWatch.Count == 1)
                {
                    throw new NoBroadcasterOrNoCampaignLeft();
                }

                continue;
            }

            if (broadcaster is null)
            {
                twitchUser.Logger.Log("No broadcaster found for this campaign.");
                thingsToWatch.Remove(campaign);
                continue;
            }

            dropCurrentSession = await CheckDropCurrentSession(broadcaster, campaign);

            if (dropCurrentSession is null)
            {
                thingsToWatch.Remove(campaign);
                continue;
            }

            if (dropCurrentSession.CurrentMinutesWatched > dropCurrentSession.requiredMinutesWatched)
            {
                twitchUser.Logger.Log("CurrentMinutesWatched > requiredMinutesWatched, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            if (string.IsNullOrEmpty(dropCurrentSession.DropId))
            {
                twitchUser.Logger.Log("DropId is null or empty, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            if (dropCurrentSession.Channel.Id != broadcaster.Id)
            {
                twitchUser.Logger.Log(
                    $"DropCurrentSession found but not the right channel ({dropCurrentSession.Channel.Name} instead of {broadcaster.Login}), changing...");
                dropCurrentSession = await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
                if (dropCurrentSession is null)
                {
                    twitchUser.Logger.Log("Can't fetch new current drop session");
                    thingsToWatch.Remove(campaign);
                    continue;
                }

            }

            timeBasedDrop = campaign.FindTimeBasedDrop(dropCurrentSession.DropId);

            if (timeBasedDrop is null)
            {
                twitchUser.Logger.Log("Time based drop not found, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            twitchUser.Logger.Log($"Time based drops : {timeBasedDrop.Name}");
        } while (timeBasedDrop is null || dropCurrentSession is null || broadcaster is null || campaign is null);


        twitchUser.CurrentTimeBasedDrop = timeBasedDrop;
        twitchUser.CurrentCampaign = campaign;
        twitchUser.CurrentBroadcaster = broadcaster;
        twitchUser.CurrentDropCurrentSession = dropCurrentSession;

        twitchUser.Status = BotStatus.Watching;
        twitchUser.Logger.Log($"Current drop campaign: {campaign.Name} ({campaign.Game.DisplayName}), watching {broadcaster.Login} | {broadcaster.Id}");
        await WatchStreamAsync(broadcaster, dropCurrentSession, campaign);
        if (campaign is RewardCampaignsAvailableToUser)
        {
            //await campaign.NotifiateAsync(twitchUser);    
        }
    }

    private async Task<DropCurrentSession?> CheckDropCurrentSession(AbstractBroadcaster broadcaster,
        AbstractCampaign campaign)
    {
        var dropCurrentSession = await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

        if (dropCurrentSession is null)
        {
            twitchUser.Logger.Log("No drop current session found, skipping");
            return null;
        }

        if (string.IsNullOrEmpty(dropCurrentSession.DropId) || dropCurrentSession.CurrentMinutesWatched == dropCurrentSession.requiredMinutesWatched)
        {
            await twitchUser.WatchManager.FakeWatchAsync(broadcaster, config.AttemptToWatch);
            dropCurrentSession = await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
        }

        return dropCurrentSession;
    }

    private async Task WatchStreamAsync(AbstractBroadcaster broadcaster, DropCurrentSession dropCurrentSession,
        AbstractCampaign campaign,
        int? minutes = null)
    {
        var stuckCounter = 0;
        var previousMinuteWatched = 0;
        var minuteWatched = dropCurrentSession.CurrentMinutesWatched;
        var requiredMinutesToWatch = dropCurrentSession.requiredMinutesWatched;
        
        while (minuteWatched <
               (minutes ?? requiredMinutesToWatch) ||
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
            
            if (string.IsNullOrEmpty(dropCurrentSession.DropId))
            {
                if (requiredMinutesToWatch - previousMinuteWatched <= 2)
                {
                    break;
                }
                
                twitchUser.Logger.Log("No drop current session found");
                //Check if the stream still alive

                var broadcasterData = twitchUser.GqlRequest.FetchStreamInformationAsync(broadcaster.Login);
                    
                if (broadcasterData?.Result is null)
                {
                    throw new System.Exception("No broadcaster data found");
                }

                if (!broadcasterData.Result.IsLive())
                {
                    twitchUser.WatchManager.Close();
                    throw new StreamOffline();
                }
                
                twitchUser.WatchManager.Close();
                throw new CurrentDropSessionChanged();
            }
            
            previousMinuteWatched = minuteWatched;

            twitchUser.Logger.Log(
                $"Waiting 20 seconds... {minuteWatched}/{requiredMinutesToWatch} minutes watched.");

            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        twitchUser.WatchManager.Close();
    }

    private async Task<(AbstractCampaign? campaign, AbstractBroadcaster? broadcaster)> SelectBroadcasterAsync(
        List<AbstractCampaign> campaigns, Inventory inventory)
    {
        AbstractBroadcaster? broadcaster = null;

        if (config.AvoidCampaign.Count > 0)
        {
            campaigns.RemoveAll(x => config.AvoidCampaign.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
        }

        foreach (var campaign in campaigns.ToList())
        {
            if (campaign.Game is null)
            {
                twitchUser.Logger.Log($"Skipping campaign {campaign.Name} because game is null.");
                continue;
            }

            CheckCancellation();
            twitchUser.Logger.Log($"Checking {campaign.Game.DisplayName} ({campaign.Name})...");

            var tempDropCampaign = await twitchUser.GqlRequest.FetchTimeBasedDropsAsync(campaign.Id);
            campaign.TimeBasedDrops = tempDropCampaign.TimeBasedDrops;
            campaign.Game = tempDropCampaign.Game;
            campaign.Allow = tempDropCampaign.Allow;

            try
            {
                var isCompleted = campaign.IsCompleted(inventory);
                if (isCompleted)
                {
                    twitchUser.Logger.Log($"Campaign {campaign.Name} already completed, skipping");
                    campaigns.Remove(campaign);
                    continue;
                }
            }
            catch (System.Exception e)
            {
                twitchUser.Logger.Error(e.Message);
            }

            if (campaign is DropCampaign dropCampaign)
            {
                if (!dropCampaign.TimeBasedDrops.Any())
                {
                    twitchUser.Logger.Log(
                        $"No time based drops found for this campaign ({dropCampaign.Name}), skipping.");
                    campaigns.Remove(campaign);
                    continue;
                }
            }

            var channels = campaign.Allow?.Channels;

            if (channels is not null)
            {
                var channelGroups = channels.Select(x => x.Name).Chunk(10).ToList();

                foreach (var channelGroup in channelGroups)
                {
                    CheckCancellation();
                    var tempBroadcasters = await twitchUser.GqlRequest.FetchStreamInformationAsync(channelGroup);

                    if (tempBroadcasters is null)
                    {
                        continue;
                    }

                    // from tempBroadcasters, select the first one that is live and that have the right game
                    var tempBroadcaster = tempBroadcasters.FirstOrDefault(tempBroadcaster =>
                        tempBroadcaster.IsLive() && tempBroadcaster.BroadcastSettings.Game?.Id != null &&
                        (campaign.Game.DisplayName == "Special Events" ||
                         tempBroadcaster.BroadcastSettings.Game.Id == campaign.Game.Id));

                    if (tempBroadcaster is null)
                    {
                        if (channelGroup == channelGroups.Last())
                        {
                            if (AppConfig.Instance.ForceTryWithTags)
                            {
                                twitchUser.Logger.Log(
                                    "No live broadcaster found in this group of channels. Forcing with stream tags");
                            }
                            else
                            {
                                twitchUser.Logger.Log($"No live broadcaster found in this group of channels. ({channelGroups.IndexOf(channelGroup) + 1}/{channelGroups.Count})");
                                return (campaign, tempBroadcaster);
                            }
                        }
                        else
                        {
                            twitchUser.Logger.Log(
                                $"No live broadcaster found in this group of channels. trying next group... ({channelGroups.IndexOf(channelGroup) + 1}/{channelGroups.Count})");
                            await Task.Delay(TimeSpan.FromSeconds(2));
                        }

                        continue;
                    }

                    broadcaster = tempBroadcaster;
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
                        twitchUser.CurrentTimeBasedDrop = timeBasedDrop;
                        await dropCampaignInProgress.NotifiateAsync(twitchUser);
                        await Task.Delay(TimeSpan.FromSeconds(20));
                    }
                }
            }
        }
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