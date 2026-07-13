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

        finishedCampaigns.RemoveAll(campaign =>
            campaign.EndAt.HasValue && campaign.EndAt.Value.ToLocalTime().AddHours(1) < now);

        Logger.LogInformation($"Removing {finishedCampaigns.Count} finished campaigns...");
        thingsToWatch.RemoveAll(campaign => finishedCampaigns.Any(finished => finished.Id == campaign.Id));

        BotUser.Inventory = inventory;
        BotUser.Status = BotStatus.Seeking;

        await CheckForClaim(inventory);

        if (thingsToWatch.Count == 0)
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

        var favouriteCampaigns = thingsToWatch.Where(x => x.Game.IsFavorite).ToList();
        thingsToWatch.RemoveAll(x => favouriteCampaigns.Contains(x));

        // Re order favouriteCampaigns to match the config file order

        favouriteCampaigns = favouriteCampaigns
            .Where(x => x.Game is not null)
            .OrderBy(x =>
            {
                if (!x.Game!.IsFavorite)
                    return int.MaxValue;

                var idx = favoriteGameNames.IndexOf(x.Game.DisplayName);
                return idx == -1 ? int.MaxValue : idx;
            })
            .ThenBy(x => x.GetEndDate())
            .ToList();


        thingsToWatch = thingsToWatch.OrderBy(x => x.GetEndDate()).ToList();

        // Apply custom sort

        //todo : Add switch here with rules
        favouriteCampaigns = favouriteCampaigns.OrderBy(x => x.GetEndDate()).ToList();
        thingsToWatch = thingsToWatch.OrderBy(x => x.GetEndDate()).ToList();

        // End custom sort

        thingsToWatch = favouriteCampaigns.Concat(thingsToWatch).ToList();

        TimeBasedDrop? timeBasedDrop = null;
        DropCurrentSession? dropCurrentSession = null;
        DropsRewardGroup? dropCurrentRewardGroup = null;
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


            /*
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
            */

            dropCurrentRewardGroup = await CheckDropProgress(broadcaster, campaign);

            if (dropCurrentRewardGroup is null)
            {
                thingsToWatch.Remove(campaign);
                continue;
            }

            if (dropCurrentRewardGroup.Self.CurrentMinutesWatched >
                dropCurrentRewardGroup.ProgressCriteria.Requirements.MinutesWatched)
            {
                Logger.LogInformation("CurrentMinutesWatched > requiredMinutesWatched, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            if (string.IsNullOrEmpty(dropCurrentRewardGroup.Id))
            {
                Logger.LogInformation("DropId is null or empty, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            timeBasedDrop = campaign.FindTimeBasedDrop(dropCurrentRewardGroup.Id);

            if (timeBasedDrop is null)
            {
                Logger.LogInformation("Time based drop not found, skipping");
                thingsToWatch.Remove(campaign);
                continue;
            }

            Logger.LogInformation($"Time based drops : {timeBasedDrop.Name}");
            // } while (timeBasedDrop is null || dropCurrentSession is null || broadcaster is null || campaign is null);
        } while (timeBasedDrop is null || dropCurrentRewardGroup is null || broadcaster is null || campaign is null);


        BotUser.CurrentTimeBasedDrop = timeBasedDrop;
        BotUser.CurrentCampaign = campaign;
        BotUser.CurrentBroadcaster = broadcaster;
        BotUser.CurrentDropCurrentSession = dropCurrentSession;

        BotUser.Status = BotStatus.Watching;
        Logger.LogInformation(
            $"Current drop campaign: {campaign.Name} ({campaign.Game?.DisplayName}), watching {broadcaster.Login} | {broadcaster.Id}");
        await WatchStreamAsync(broadcaster, dropCurrentRewardGroup, campaign);

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
            await BotUser.WatchManager.FakeWatchAsync(broadcaster, campaign.Game,
                BotSettings.CurrentValue.AttemptToWatch);
            dropCurrentSession = await BotUser.TwitchRepository.FetchCurrentSessionContextAsync(broadcaster);
        }

        return dropCurrentSession;
    }

    private async Task<DropsCampaign?> FetchCampaignProgress(User broadcaster, AbstractCampaign campaign)
    {
        try
        {
            var campaignsProgress = await BotUser.TwitchRepository.FetchDropCampaignsProgressAsync(broadcaster);
            return campaignsProgress.FirstOrDefault(x => x.Id == campaign.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching campaign progress");
            return null;
        }
    }

    private async Task<List<DropProgressInfo>> GetTwitchProgressListAsync(User broadcaster, AbstractCampaign campaign, DropsRewardGroup dropCurrentRewardGroup)
    {
        var list = new List<DropProgressInfo>();
        try
        {
            var campaignProgress = await FetchCampaignProgress(broadcaster, campaign);
            if (campaignProgress?.RewardGroups != null)
            {
                foreach (var group in campaignProgress.RewardGroups)
                {
                    list.Add(new DropProgressInfo
                    {
                        Name = group.Name,
                        CurrentProgress = group.Self?.CurrentMinutesWatched ?? 0,
                        RequiredProgress = group.ProgressCriteria?.Requirements?.MinutesWatched ?? 0,
                        IsClaimed = group.Self?.Status == "CLAIMED",
                        IsActive = group.Id == dropCurrentRewardGroup.Id
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting Twitch progress list");
        }

        if (list.Count == 0)
        {
            list.Add(new DropProgressInfo
            {
                Name = dropCurrentRewardGroup.Name,
                CurrentProgress = dropCurrentRewardGroup.Self?.CurrentMinutesWatched ?? 0,
                RequiredProgress = dropCurrentRewardGroup.ProgressCriteria?.Requirements?.MinutesWatched ?? 0,
                IsClaimed = dropCurrentRewardGroup.Self?.Status == "CLAIMED",
                IsActive = true
            });
        }
        return list;
    }

    private async Task<DropsRewardGroup?> CheckDropProgress(User broadcaster, AbstractCampaign campaign)
    {
        var campaignProgress = await FetchCampaignProgress(broadcaster, campaign);
        if (campaignProgress is null) return null;

        //Remove every drop where
        var filteredCampaignProgress = campaignProgress.RewardGroups
            .Where(x => x.Self != null && x.Self.CurrentMinutesWatched < x.ProgressCriteria.Requirements.MinutesWatched)
            .OrderBy(x => x.ProgressCriteria.Requirements.MinutesWatched).ToList();

        return filteredCampaignProgress.FirstOrDefault();
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

    private async Task WatchStreamAsync(User broadcaster, DropsRewardGroup dropCurrentRewardGroup,
        AbstractCampaign campaign,
        int? minutes = null)
    {
        var stuckCounter = 0;
        var previousMinuteWatched = 0;
        var minuteWatched = dropCurrentRewardGroup.Self.CurrentMinutesWatched;
        var requiredMinutesToWatch = dropCurrentRewardGroup.ProgressCriteria.Requirements.MinutesWatched;

        if (minuteWatched.HasValue && requiredMinutesToWatch.HasValue)
        {
            var uniqueKey = $"twitch-{BotUser.Login}-{campaign.Id}";
            var itemImage = dropCurrentRewardGroup.Rewards.FirstOrDefault()?.ThumbnailURL ?? campaign.Game?.BoxArtUrl ?? string.Empty;
            var dropsProgress = await GetTwitchProgressListAsync(broadcaster, campaign, dropCurrentRewardGroup);
            await NotificationService.SendOrUpdateProgressNotification(
                BotUser,
                campaign.Game?.DisplayName ?? campaign.Game?.Name ?? "Unknown Game",
                campaign.Name ?? "Unknown Campaign",
                itemImage,
                dropsProgress,
                uniqueKey
            );
        }

        while (minuteWatched <= requiredMinutesToWatch)
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
                var newDropCurrentSession = await CheckDropProgress(broadcaster, campaign);

                if (newDropCurrentSession is null)
                {
                    Logger.LogInformation("No more campaign progress found");
                    break;
                }
                else
                {
                    dropCurrentRewardGroup = newDropCurrentSession;
                }

                // BotUser.CurrentDropCurrentSession = dropCurrentSession;
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, ex.Message);
            }

            minuteWatched = dropCurrentRewardGroup.Self.CurrentMinutesWatched;

            if (previousMinuteWatched == minuteWatched)
            {
                stuckCounter++;
            }
            else
            {
                stuckCounter = 0;
            }

            if (stuckCounter >= 10)
            {
                BotUser.WatchManager.Close();
                await BotUser.WatchManager.WatchStreamAsync(broadcaster, campaign.Game);
                await Task.Delay(TimeSpan.FromSeconds(20));

                var newDropCurrentSession = await CheckDropProgress(broadcaster, campaign);

                if (newDropCurrentSession is null)
                {
                    Logger.LogInformation("No more campaign progress found after being stuck");
                }
                else
                {
                    dropCurrentRewardGroup = newDropCurrentSession;
                }
            }

            if (string.IsNullOrEmpty(dropCurrentRewardGroup.Id))
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

            if (minuteWatched is null)
            {
                Logger.LogError("Minute watched is null");
                BotUser.WatchManager.Close();
                throw new System.Exception("Minute watched is null");
            }

            previousMinuteWatched = minuteWatched.Value;

            Logger.LogInformation(
                $"Waiting 60 seconds... {minuteWatched}/{requiredMinutesToWatch} minutes watched.");

            if (requiredMinutesToWatch.HasValue)
            {
                var uniqueKey = $"twitch-{BotUser.Login}-{campaign.Id}";
                var itemImage = dropCurrentRewardGroup.Rewards.FirstOrDefault()?.ThumbnailURL ?? campaign.Game?.BoxArtUrl ?? string.Empty;
                var dropsProgress = await GetTwitchProgressListAsync(broadcaster, campaign, dropCurrentRewardGroup);
                await NotificationService.SendOrUpdateProgressNotification(
                    BotUser,
                    campaign.Game?.DisplayName ?? campaign.Game?.Name ?? "Unknown Game",
                    campaign.Name ?? "Unknown Campaign",
                    itemImage,
                    dropsProgress,
                    uniqueKey
                );
            }

            await Task.Delay(TimeSpan.FromSeconds(60));
        }

        BotUser.WatchManager.Close();
    }

    private async Task<(AbstractCampaign? campaign, User? broadcaster)> SelectBroadcasterAsync(
        List<AbstractCampaign> campaigns, Inventory inventory)
    {
        User? broadcaster = null;

        if (TwitchSettings.AvoidCampaign.Count > 0)
        {
            /*campaigns.RemoveAll(x =>
                TwitchSettings.AvoidCampaign.Contains(x.Game?.DisplayName ?? x.Game?.Name,
                    StringComparer.OrdinalIgnoreCase));
            */
            campaigns.RemoveAll(x =>
                TwitchSettings.AvoidCampaign.Contains(x.Name,
                    StringComparer.OrdinalIgnoreCase));
        }

        foreach (var campaign in campaigns.ToList())
        {
            if (campaign.Game is null)
            {
                Logger.LogInformation("Skipping campaign {campaign.Name} because game is null.", campaign.Name);
                continue;
            }

            Logger.LogInformation("Checking {campaignGameDisplayName} ({campaignName})...", campaign.Game.DisplayName,
                campaign.Name);

            if (finishedCampaigns.Contains(campaign))
            {
                Logger.LogInformation("Campaign {campaign.Name} already completed from local list, skipping",
                    campaign.Name);
                campaigns.Remove(campaign);
                continue;
            }

            var tempDropCampaign = await BotUser.TwitchRepository.FetchTimeBasedDropsAsync(campaign.Id);
            campaign.TimeBasedDrops = tempDropCampaign.TimeBasedDrops;
            campaign.Game = tempDropCampaign.Game;
            campaign.Allow = tempDropCampaign.Allow;

            if (campaign.TimeBasedDrops.Count == 0)
            {
                Logger.LogInformation("No time based drops available for this campaign ({campaign.Name}), skipping",
                    campaign.Name);
                campaigns.Remove(campaign);
                continue;
            }

            try
            {
                var isCompleted = await campaign.IsCompleted(inventory, BotUser.TwitchRepository);
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

            // Todo: check if we got enough time
            var firstTimeBasedDrops = campaign.TimeBasedDrops.FirstOrDefault();
            if (firstTimeBasedDrops != null && campaign.EndAt.HasValue)
            {
                var minutesLeft = (campaign.EndAt.Value - DateTime.UtcNow).TotalMinutes;
                if (minutesLeft < firstTimeBasedDrops.RequiredMinutesWatched)
                {
                    Logger.LogInformation("Not enough time to watch this campaign ({campaign.Name}), skipping",
                        campaign.Name);
                    finishedCampaigns.Add(campaign);
                    campaigns.Remove(campaign);
                    continue;
                }
            }

            if (campaign is DropCampaign dropCampaign)
            {
                if (!dropCampaign.TimeBasedDrops.Any())
                {
                    Logger.LogInformation(
                        "No time based drops found for this campaign ({dropCampaign.Name}), skipping.",
                        dropCampaign.Name);
                    campaigns.Remove(campaign);
                    continue;
                }
            }

            var channels = campaign.Allow?.Channels;
            

            if (channels is not null && channels.Count < 250)
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
                                    "No live broadcaster found in this group of channels. ({currentGroup}/{channelGroups.Count})",
                                    (channelGroups.IndexOf(channelGroup) + 1), channelGroups.Count);
                                return (campaign, tempBroadcaster);
                            }
                        }
                        else
                        {
                            Logger.LogInformation(
                                "No live broadcaster found in this group of channels. trying next group... ({currentGroup}/{channelGroups.Count})",
                                (channelGroups.IndexOf(channelGroup) + 1), channelGroups.Count);
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
                    Logger.LogError("Self in TimeBasedDrop is null for {dropCampaignInProgress.Name}",
                        dropCampaignInProgress.Name);
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

                        var firstBenefit = timeBasedDrop.BenefitEdges.FirstOrDefault();
                        if (firstBenefit is not null)
                        {
                            var progressKey = $"twitch-{BotUser.Login}-{dropCampaignInProgress.Id}";
                            await NotificationService.UpdateProgressMessageAsClaimedAsync(
                                BotUser, progressKey, dropCampaignInProgress.Game.Name, firstBenefit.Benefit.Name, firstBenefit.Benefit.ImageAssetURL);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(20));
                }
            }
        }

        var earnedDropRewardToClaim = inventory.EarnedDropRewards.Edges.Where(x => x.Node.Status != "CLAIMED").ToList();
        
        foreach (var earnedDropRewardEdge in earnedDropRewardToClaim)
        {
            if (earnedDropRewardEdge.Node.Item.DistributionType != DistributionType.CODE)
            {
                continue;
            }
            
            var rewardCampaignCode = await BotUser.TwitchRepository.RewardCodeModal(earnedDropRewardEdge.Node.Campaign.Id, earnedDropRewardEdge.Node.Id);
            var message =
                $"```{rewardCampaignCode.Value}``` has been rewarded for {earnedDropRewardEdge.Node.Item.Name}`";
            await NotificationService.SendNotification(BotUser, message, earnedDropRewardEdge.Node.Item.ThumbnailURL,
                new Uri(earnedDropRewardEdge.Node.Item.RedemptionURL));

            var progressKey = $"twitch-{BotUser.Login}-{earnedDropRewardEdge.Node.Campaign.Id}";
            await NotificationService.UpdateProgressMessageAsClaimedAsync(
                BotUser, progressKey, earnedDropRewardEdge.Node.Campaign.Game?.Name ?? earnedDropRewardEdge.Node.Campaign.Name, earnedDropRewardEdge.Node.Item.Name, earnedDropRewardEdge.Node.Item.ThumbnailURL);

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}