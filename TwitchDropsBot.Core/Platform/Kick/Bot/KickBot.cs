using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Kick.Bot;

public class KickBot : BaseBot<KickUser>
{
    public KickBot(KickUser user, ILogger logger, NotificationService notificationService, IOptionsMonitor<BotSettings> botSettings) : base(user,
        logger, notificationService, botSettings)
    {
    }

    public override List<String> GetUserFavoriteGames()
    {
        var user = BotSettings.CurrentValue.KickSettings.KickUsers.Find(user => user.Id == BotUser.Id);

        return user.FavouriteGames;
    }

    protected override async Task StartAsync()
    {
        var inventory = await BotUser.KickRepository.GetInventory();
        var thingsToWatch = await BotUser.KickRepository.GetDropsCampaignsAsync();

        var finishedCampaigns = inventory.FindAll(x => x.Status == "claimed");
        Logger.LogInformation($"Removing {finishedCampaigns.Count} finished campaigns...");
        thingsToWatch.RemoveAll(campaign => finishedCampaigns.Any(finished => finished.Id == campaign.Id));

        if (thingsToWatch.Count == 0)
        {
            Logger.LogError("No campaigns to watch found.");
            throw new NoBroadcasterOrNoCampaignLeft();
        }

        thingsToWatch = thingsToWatch
            .OrderBy(x => x.Channels.Count == 0)
            .ToList();

        await CheckForClaim(inventory);

        if (BotUser.OnlyFavouriteGames)
        {
            thingsToWatch.RemoveAll(x => !x.Category?.IsFavorite ?? false);
        }

        var (campaign, broadcaster) = await SelectBroadcasterAsync(thingsToWatch, inventory);

        if (campaign is null)
        {
            Logger.LogError("No campaign found.");
            throw new NoBroadcasterOrNoCampaignLeft();
        }

        if (broadcaster is null)
        {
            Logger.LogError("No broadcaster found for this campaign");
            thingsToWatch.Remove(campaign);
            return;
        }

        // Remove all Rewards from thingsToWatch that are already claimed in the inventory list
        var reward = campaign.Rewards.First();

        if (reward is null)
        {
            Logger.LogError("Reward is null");
            throw new Exception("Reward is null");
        }

        await FakeWatchStreamAsync(broadcaster, campaign);

        Logger.LogInformation($"Time based drops : {reward.Name}");
        Logger.LogInformation(
            $"Current drop campaign: {campaign.Name} ({campaign.Category.Name}), watching {broadcaster.slug} | {broadcaster.Id}");
        await WatchStreamAsync(broadcaster, campaign, reward);
    }

    private async Task FakeWatchStreamAsync(Channel broadcaster, Campaign campaign)
    {
        var summary = await BotUser.KickRepository.GetSummary(campaign);
        if (summary is not null)
        {
            return;
        }

        while (summary is null)
        {
            Logger.LogInformation("Trying to init the drop...");
            await BotUser.WatchManager.WatchStreamAsync(broadcaster, campaign.Category);
            await Task.Delay(TimeSpan.FromSeconds(60));
            summary = await BotUser.KickRepository.GetSummary(campaign);
        }

        BotUser.WatchManager.Close();
    }

    private async Task WatchStreamAsync(Channel broadcaster, Campaign campaign, Reward reward, int? minutes = null)
    {
        var summary = await BotUser.KickRepository.GetSummary(campaign);
        var stuckCounter = 0;
        double previousMinuteWatched = 0;
        var minuteWatched = summary.ProgressUnits;

        var requiredMinutesToWatch = reward.RequiredUnits;

        while (minuteWatched <
               (minutes ?? requiredMinutesToWatch)) // While all the drops are not claimed
        {
            try
            {
                await BotUser.WatchManager
                    .WatchStreamAsync(broadcaster, campaign.Category); // If not live, it will throw a 404 error    
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                BotUser.WatchManager.Close();
                throw new StreamOffline();
            }

            summary = await BotUser.KickRepository.GetSummary(campaign);

            if (summary is null)
            {
                BotUser.WatchManager.Close();
                throw new Exception("Summary is null");
            }

            minuteWatched = summary.ProgressUnits;

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
                throw new Exception();
            }

            previousMinuteWatched = minuteWatched;

            Logger.LogInformation(
                $"Waiting 60 seconds... {minuteWatched}/{requiredMinutesToWatch} minutes watched.");

            await Task.Delay(TimeSpan.FromSeconds(60));
        }

        BotUser.WatchManager.Close();
    }

    private async Task<(Campaign? campaign, Channel? broadcaster)> SelectBroadcasterAsync(List<Campaign> campaigns,
        List<Campaign> inventory)
    {
        foreach (var campaign in campaigns.ToList())
        {
            Logger.LogInformation($"Checking {campaign.Category.Name} ({campaign.Name})...");

            var matchingCampaignInventory = inventory.Find(x => x.Id == campaign.Id);

            if (matchingCampaignInventory is not null)
            {
                var claimedRewards = matchingCampaignInventory.Rewards.FindAll(x => x.Claimed || x.Progress == 1);
                campaign.Rewards.RemoveAll(r => claimedRewards.Contains(r));
            }

            if (campaign.Rewards.Count == 0)
            {
                Logger.LogInformation($"No rewards available for {campaign.Name}, skipping...");
                campaigns.Remove(campaign);
                continue;
            }

            var channels = campaign.Channels;

            if (channels.Count > 0)
            {
                var channelsResult = await BotUser.KickRepository.GetLivestreamCampaignsAsync(campaign);

                if (!channelsResult.Any())
                {
                    campaigns.Remove(campaign);
                    continue;
                }

                var mostViewers = channelsResult.OrderBy(x => x.ViewerCount).FirstOrDefault();

                if (mostViewers is null)
                {
                    campaigns.Remove(campaign);
                    continue;
                }

                return (campaign, mostViewers.Channel);
            }

            // var livestreams = await BotUser.KickHttpClient.FindStreams(campaign);
            var livestreams = await BotUser.KickRepository.GetLivestreamCampaignsAsync(campaign);

            var mostViewerLiveStream = livestreams.FirstOrDefault();

            if (mostViewerLiveStream is null)
            {
                campaigns.Remove(campaign);
                continue;
            }

            return (campaign, mostViewerLiveStream.Channel);
        }

        return (null, null);
    }

    private async Task CheckForClaim(List<Campaign> campaigns)
    {
        foreach (var campaign in campaigns)
        {
            foreach (var reward in campaign.Rewards)
            {
                //fixme: check if works
                if (!reward.Claimed && reward.Progress == 1 &&
                    !BotUser.KickRepository.NoClaimCategories.Contains(campaign.Category))
                {
                    try
                    {
                        await BotUser.KickRepository.ClaimDrop(campaign, reward);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(
                            "Can't claim {reward.Name} for the campaign {campaign.Name}, account not linked please restart the bot once done...",
                            reward.Name, campaign.Name);
                        var message =
                            $"Can't claim {reward.Name} for the campaign {campaign.Name}, account not linked please restart the bot once done...";
                        await NotifyError("CLAIM ERROR", message, $"https://ext.cdn.kick.com/{reward.ImageUrl}");
                        await Task.Delay(TimeSpan.FromSeconds(2));
                        continue;
                    }
                    
                    await NotificationService.SendNotification(BotUser, campaign.Category.Name, reward.Name, $"https://ext.cdn.kick.com/{reward.ImageUrl}");
                }
            }
        }
    }
}