using Discord;
using Serilog;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;

namespace TwitchDropsBot.Core.Platform.Kick.Bot;

public class KickBot : BaseBot<KickUser>
{
    public KickBot(KickUser user, ILogger logger) : base(user, logger)
    {
        Logger = Logger.ForContext("UserType", user.GetType().Name).ForContext("User", user.Login);
    }
    
    public override async Task StartAsync()
    {
        var inventory = await BotUser.KickRepository.GetInventory();
        var thingsToWatch = await BotUser.KickRepository.GetDropsCampaignsAsync();

        var finishedCampaigns = inventory.Where(x => x.Status == "claimed").ToList();
        thingsToWatch.RemoveAll(tw => finishedCampaigns.Any(fc => fc.Id == tw.Id));
        
        if (thingsToWatch.Count == 0)
        {
            Logger.Error("No campaigns to watch found.");
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
            Logger.Error("No campaign found.");
            throw new NoBroadcasterOrNoCampaignLeft();
        }

        if (broadcaster is null)
        {
            Logger.Error("No broadcaster found for this campaign");
            thingsToWatch.Remove(campaign);
            return;
        }

        // Remove all Rewards from thingsToWatch that are already claimed in the inventory list
        var reward = campaign.Rewards.First();

        if (reward is null)
        {
            Logger.Error("Reward is null");
            throw new Exception("Reward is null");
        }

        Logger.Information($"Time based drops : {reward.Name}");
        Logger.Information(
            $"Current drop campaign: {campaign.Name} ({campaign.Category.Name}), watching {broadcaster.slug} | {broadcaster.Id}");
        await WatchStreamAsync(broadcaster, campaign, reward);
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
                Logger.Error(ex, ex.Message);
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

            Logger.Information(
                $"Waiting 60 seconds... {minuteWatched}/{requiredMinutesToWatch} minutes watched.");

            await Task.Delay(TimeSpan.FromSeconds(60));
        }

        BotUser.WatchManager.Close();
    }

    private async Task<(Campaign? campaign, Channel? broadcaster)> SelectBroadcasterAsync(List<Campaign> campaigns, List<Campaign> inventory)
    {
        foreach (var campaign in campaigns.ToList())
        {
            Logger.Information($"Checking {campaign.Category.Name} ({campaign.Name})...");
            
            var matchingCampaignInventory = inventory.Find(x => x.Id == campaign.Id);

            if (matchingCampaignInventory is null)
            {
                Logger.Error("matchingCampaignInventory is null");
                campaigns.Remove(campaign);
                continue;
            }
            
            var claimedRewards = matchingCampaignInventory.Rewards.FindAll(x => x.Claimed);
            campaign.Rewards.RemoveAll(r => claimedRewards.Contains(r));

            if (campaign.Rewards.Count == 0)
            {
                Logger.Information($"No rewards available for {campaign.Name}, skipping...");
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
                if (!reward.Claimed && reward.Progress == 1)
                {
                    await BotUser.KickRepository.ClaimDrop(campaign, reward);
                    await NotificationServices.SendNotification(BotUser, campaign.Category.Name,
                        reward.Name, $"https://ext.cdn.kick.com/{reward.ImageUrl}");
                }
            }
        }
    }
}