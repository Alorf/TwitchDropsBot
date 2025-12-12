using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Models;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;
using TwitchDropsBot.Core.Platform.Twitch.Models.Extensions;
using TwitchDropsBot.Core.Platform.Twitch.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Utils;

namespace TwitchDropsBot.Core.Platform.Twitch.Bot;

public class TwitchBot : BaseBot<TwitchUser>
{
    // Only way for now to get refresh settings for each run
    private TwitchSettings TwitchSettings
    {
        get
        {
            if (_botSettings is null)
            {
                throw new Exception("BotSettings is null");
            }
            
            return _botSettings.CurrentValue.TwitchSettings;
        }
    }

    private List<CompletedRewardCampaigns> claimedReward;
    private List<AbstractCampaign> finishedCampaigns;
    private IOptionsMonitor<BotSettings> _botSettings;
    private List<string> _gamesToCheck;

    public TwitchBot(
        TwitchUser user,
        ILogger logger,
        NotificationService notificationService,
        IOptionsMonitor<BotSettings> botSettings
        ) : base(user, logger, notificationService, botSettings)
    {
        claimedReward = new List<CompletedRewardCampaigns>();
        finishedCampaigns = new List<AbstractCampaign>();
        
        _botSettings = botSettings;

        _gamesToCheck = new List<string>();
    }

    public override List<String> GetUserFavoriteGames()
    {
        var user = BotSettings.CurrentValue.TwitchSettings.TwitchUsers.Find(user => user.Id == BotUser.Id);

        return user.FavouriteGames;
    }

    protected override async Task StartAsync()
    {
        // Refresh data
        var userFavoriteGames = GetUserFavoriteGames();
        
        _gamesToCheck = userFavoriteGames.Count > 0
            ? userFavoriteGames
            : _botSettings.CurrentValue.FavouriteGames;
        
        BotUser.OnlyFavouriteGames = TwitchSettings.OnlyFavouriteGames;
        BotUser.OnlyConnectedAccounts = TwitchSettings.OnlyConnectedAccounts;

        // Get campaigns
        var thingsToWatch = await BotUser.TwitchRepository.FetchDropsAsync();
        var inventory = await BotUser.TwitchRepository.FetchInventoryDropsAsync();
        DateTime now = DateTime.Now;
        
        finishedCampaigns.RemoveAll( campaign => campaign.EndAt.Value.ToLocalTime().AddHours(1) < now);
        
        Logger.LogInformation($"Removing {finishedCampaigns.Count} finished campaigns...");
        thingsToWatch.RemoveAll(campaign => finishedCampaigns.Any(finished => finished.Id == campaign.Id));


        BotUser.Inventory = inventory;
        BotUser.Status = BotStatus.Seeking;

        await CheckForClaim(inventory);

        if (thingsToWatch.Count == 0 )
        {
            throw new NoBroadcasterOrNoCampaignLeft();
        }

        if (BotUser.OnlyConnectedAccounts)
        {
            thingsToWatch.RemoveAll(x =>
                x is DropCampaign dropCampaign && !dropCampaign.Self.IsAccountConnected &&
                dropCampaign.AccountLinkURL != "https://twitch.tv/");
        }

        if (BotUser.OnlyFavouriteGames)
        {
            thingsToWatch.RemoveAll(x => !x.Game?.IsFavorite ?? false);
        }

        // Assuming you have a list of favorite game names
        var favoriteGameNames = _gamesToCheck;

        // Order things to watch by the order of favorite game names and drop that is ending soon
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
        User? broadcaster = null;
        AbstractCampaign? campaign = null;

        do
        {
            if (thingsToWatch.Count == 0)
            {
                throw new NoBroadcasterOrNoCampaignLeft();
            }

            (campaign, broadcaster) = await SelectBroadcasterAsync(thingsToWatch, inventory);

            if (campaign is null)
            {
                Logger.LogInformation("No campaign found.");
                if (thingsToWatch.Count == 1)
                {
                    throw new NoBroadcasterOrNoCampaignLeft();
                }

                continue;
            }

            if (broadcaster is null)
            {
                Logger.LogInformation("No broadcaster found for this campaign.");
                thingsToWatch.Remove(campaign);
                continue;
            }

            dropCurrentSession = await CheckDropCurrentSession(broadcaster, campaign);

            if (dropCurrentSession is null)
            {
                thingsToWatch.Remove(campaign);
                continue;
            }

            if (dropCurrentSession.CurrentMinutesWatched > dropCurrentSession.RequiredMinutesWatched)
            {
                Logger.LogInformation("CurrentMinutesWatched > requiredMinutesWatched, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            if (string.IsNullOrEmpty(dropCurrentSession.DropId))
            {
                Logger.LogInformation("DropId is null or empty, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            if (dropCurrentSession.Channel.Id != broadcaster.Id)
            {
                Logger.LogInformation(
                    $"DropCurrentSession found but not the right channel ({dropCurrentSession.Channel.Name} instead of {broadcaster.Login}), changing...");
                dropCurrentSession = await BotUser.TwitchRepository.FetchCurrentSessionContextAsync(broadcaster);
                if (dropCurrentSession is null)
                {
                    Logger.LogInformation("Can't fetch new current drop session");
                    thingsToWatch.Remove(campaign);
                    continue;
                }
            }

            timeBasedDrop = campaign.FindTimeBasedDrop(dropCurrentSession.DropId);

            if (timeBasedDrop is null)
            {
                Logger.LogInformation("Time based drop not found, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            Logger.LogInformation($"Time based drops : {timeBasedDrop.Name}");
        } while (timeBasedDrop is null || dropCurrentSession is null || broadcaster is null || campaign is null);


        BotUser.CurrentTimeBasedDrop = timeBasedDrop;
        BotUser.CurrentCampaign = campaign;
        BotUser.CurrentBroadcaster = broadcaster;
        BotUser.CurrentDropCurrentSession = dropCurrentSession;

        BotUser.Status = BotStatus.Watching;
        Logger.LogInformation(
            $"Current drop campaign: {campaign.Name} ({campaign.Game?.DisplayName}), watching {broadcaster.Login} | {broadcaster.Id}");
        await WatchStreamAsync(broadcaster, dropCurrentSession, campaign);

        if (campaign is RewardCampaign)
        {
            var notifications = await BotUser.TwitchRepository.FetchNotificationsAsync(1);

            foreach (var edge in notifications.Edges)
            {
                //Search for the first action with the type "click"
                var action = edge.Node.Actions.FirstOrDefault(x => x.Type == "click");

                await NotificationService.SendNotification(BotUser, edge.Node.Body, edge.Node.ThumbnailUrl,
                    new Uri(action.Url));
            }
        }

        Logger.LogDebug("Loop ended");
    }

    private async Task<DropCurrentSession?> CheckDropCurrentSession(User broadcaster,
        AbstractCampaign campaign)
    {
        var dropCurrentSession = await BotUser.TwitchRepository.FetchCurrentSessionContextAsync(broadcaster);

        if (dropCurrentSession is null)
        {
            Logger.LogInformation("No drop current session found, skipping");
            return null;
        }

        if (string.IsNullOrEmpty(dropCurrentSession.DropId) ||
            dropCurrentSession.CurrentMinutesWatched == dropCurrentSession.RequiredMinutesWatched)
        {
            await BotUser.WatchManager.FakeWatchAsync(broadcaster, campaign.Game, BotSettings.CurrentValue.AttemptToWatch);
            dropCurrentSession = await BotUser.TwitchRepository.FetchCurrentSessionContextAsync(broadcaster);
        }

        return dropCurrentSession;
    }

    private async Task WatchStreamAsync(User broadcaster, DropCurrentSession dropCurrentSession,
        AbstractCampaign campaign,
        int? minutes = null)
    {
        var stuckCounter = 0;
        var previousMinuteWatched = 0;
        var minuteWatched = dropCurrentSession.CurrentMinutesWatched;
        var requiredMinutesToWatch = dropCurrentSession.RequiredMinutesWatched;

        while (minuteWatched <
               (minutes ?? requiredMinutesToWatch) ||
               dropCurrentSession.RequiredMinutesWatched == 0) // While all the drops are not claimed
        {
            try
            {
                await BotUser.WatchManager
                    .WatchStreamAsync(broadcaster, campaign.Game); // If not live, it will throw a 404 error    
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                BotUser.WatchManager.Close();
                throw new StreamOffline();
            }

            try
            {
                var newDropCurrentSession =
                    await BotUser.TwitchRepository.FetchCurrentSessionContextAsync(broadcaster);

                if (newDropCurrentSession is null)
                {
                    Logger.LogInformation("Can't fetch new current drop session");
                }
                else
                {
                    dropCurrentSession = newDropCurrentSession;
                }

                BotUser.CurrentDropCurrentSession = dropCurrentSession;
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, ex.Message);
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
                BotUser.WatchManager.Close();
                await BotUser.WatchManager.WatchStreamAsync(broadcaster, campaign.Game);
                await Task.Delay(TimeSpan.FromSeconds(20));

                var newDropCurrentSession =
                    await BotUser.TwitchRepository.FetchCurrentSessionContextAsync(broadcaster);

                if (newDropCurrentSession is null)
                {
                    Logger.LogInformation("Can't fetch new current drop session after being stuck");
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

                Logger.LogInformation("No drop current session found");
                //Check if the stream still alive

                var broadcasterData = BotUser.TwitchRepository.FetchStreamInformationAsync(broadcaster.Login);

                if (broadcasterData?.Result is null)
                {
                    throw new System.Exception("No broadcaster data found");
                }

                if (!broadcasterData.Result.IsLive())
                {
                    BotUser.WatchManager.Close();
                    throw new StreamOffline();
                }

                BotUser.WatchManager.Close();
                throw new CurrentDropSessionChanged();
            }

            previousMinuteWatched = minuteWatched;

            Logger.LogInformation(
                $"Waiting 20 seconds... {minuteWatched}/{requiredMinutesToWatch} minutes watched.");

            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        BotUser.WatchManager.Close();
    }

    private async Task<(AbstractCampaign? campaign, User? broadcaster)> SelectBroadcasterAsync(
        List<AbstractCampaign> campaigns, Inventory inventory)
    {
        User? broadcaster = null;

        if (TwitchSettings.AvoidCampaign.Count > 0)
        {
            campaigns.RemoveAll(x => TwitchSettings.AvoidCampaign.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
        }

        foreach (var campaign in campaigns.ToList())
        {
            if (campaign.Game is null)
            {
                Logger.LogInformation("Skipping campaign {campaign.Name} because game is null.", campaign.Name);
                continue;
            }

            Logger.LogInformation("Checking {campaignGameDisplayName} ({campaignName})...", campaign.Game.DisplayName, campaign.Name);

            if (finishedCampaigns.Contains(campaign))
            {
                Logger.LogInformation("Campaign {campaign.Name} already completed from local list, skipping", campaign.Name);
                campaigns.Remove(campaign);
                continue;
            }

            var tempDropCampaign = await BotUser.TwitchRepository.FetchTimeBasedDropsAsync(campaign.Id);
            campaign.TimeBasedDrops = tempDropCampaign.TimeBasedDrops;
            campaign.Game = tempDropCampaign.Game;
            campaign.Allow = tempDropCampaign.Allow;

            if (campaign.TimeBasedDrops.Count == 0)
            {
                Logger.LogInformation("No time based drops available for this campaign ({campaign.Name}), skipping", campaign.Name);
                finishedCampaigns.Add(campaign);
                campaigns.Remove(campaign);
                continue;
            }

            try
            {
                var isCompleted = campaign.IsCompleted(inventory, BotUser.TwitchRepository);
                if (isCompleted)
                {
                    Logger.LogInformation("Campaign {campaign.Name} already completed, skipping", campaign.Name);
                    finishedCampaigns.Add(campaign);
                    campaigns.Remove(campaign);
                    continue;
                }
            }
            catch (System.Exception e)
            {
                Logger.LogError(e, e.Message);
            }

            if (campaign is DropCampaign dropCampaign)
            {
                if (!dropCampaign.TimeBasedDrops.Any())
                {
                    Logger.LogInformation(
                        "No time based drops found for this campaign ({dropCampaign.Name}), skipping.", dropCampaign.Name);
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
                    var tempBroadcasters = await BotUser.TwitchRepository.FetchStreamInformationAsync(channelGroup);

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
                            if (_botSettings.CurrentValue.TwitchSettings.ForceTryWithTags)
                            {
                                Logger.LogInformation(
                                    "No live broadcaster found in this group of channels. Forcing with stream tags");
                            }
                            else
                            {
                                Logger.LogInformation(
                                    "No live broadcaster found in this group of channels. ({currentGroup}/{channelGroups.Count})", (channelGroups.IndexOf(channelGroup) + 1), channelGroups.Count);
                                return (campaign, tempBroadcaster);
                            }
                        }
                        else
                        {
                            Logger.LogInformation(
                                "No live broadcaster found in this group of channels. trying next group... ({currentGroup}/{channelGroups.Count})", (channelGroups.IndexOf(channelGroup) + 1), channelGroups.Count);
                            await Task.Delay(TimeSpan.FromSeconds(2));
                        }

                        continue;
                    }

                    broadcaster = tempBroadcaster;
                    return (campaign, broadcaster);
                }
            }

            // Search for channel that potentially have the drops
            var game = await BotUser.TwitchRepository.FetchDirectoryPageGameAsync(campaign.Game.Slug,
                campaign is DropCampaign);

            if (game is null)
            {
                Logger.LogInformation("No game found for slug {campaign.Game.Slug}.", campaign.Game.Slug);
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
        if (inventory is null)
        {
            return;
        }

        // For every timebased drop, check if it is claimed
        foreach (var dropCampaignInProgress in inventory.DropCampaignsInProgress)
        {
            foreach (var timeBasedDrop in dropCampaignInProgress.TimeBasedDrops)
            {
                if (timeBasedDrop.Self is null)
                {
                    Logger.LogError("Self in TimeBasedDrop is null for {dropCampaignInProgress.Name}", dropCampaignInProgress.Name);
                    return;
                }

                if (timeBasedDrop.Self.IsClaimed == false && timeBasedDrop.Self?.DropInstanceID != null)
                {
                    await BotUser.TwitchRepository.ClaimDropAsync(timeBasedDrop.Self.DropInstanceID);
                    if (dropCampaignInProgress.Game?.Name != null && timeBasedDrop.Name is not null)
                    {
                        foreach (var benefitEdge in timeBasedDrop.BenefitEdges)
                        {
                            await NotificationService.SendNotification(BotUser, dropCampaignInProgress.Game.Name,
                                benefitEdge.Benefit.Name, benefitEdge.Benefit.ImageAssetURL);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(20));
                }
            }
        }
        
        var newClaimedReward = inventory.CompletedRewardCampaigns;
        
        if (claimedReward.Count == 0)
        {
            claimedReward = newClaimedReward;    
        }
        
        List<CompletedRewardCampaigns> newlyClaimedReward = newClaimedReward.Except(claimedReward).ToList();
        foreach (var rewardCampaign in newlyClaimedReward)
        {
            foreach (var reward in rewardCampaign.Rewards)
            {
                var rewardCampaignCode = await BotUser.TwitchRepository.RewardCodeModal(rewardCampaign.Id, reward.Id);
                var message = $"```{rewardCampaignCode.Value}``` has been rewarded for {reward.Name}`\n Claim before <t:{((DateTimeOffset) reward.EarnableUntil).ToUnixTimeSeconds()}>";
                await NotificationService.SendNotification(BotUser, message, reward.ThumbnailImage.Image1xURL, new Uri(rewardCampaign.ExternalURL));
                await Task.Delay(TimeSpan.FromSeconds(5));   
            }
        }

        claimedReward = newClaimedReward;
    }
}