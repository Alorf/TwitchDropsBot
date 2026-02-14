
using System.Text.Json.Serialization;
using Discord;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;
using TwitchDropsBot.Core.Platform.Twitch.Repository;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class DropCampaign : AbstractCampaign
{

    public override async Task<bool> IsCompleted(Inventory inventory, TwitchGqlRepository repository)
    {

        var dropsCampaignInProgress = inventory.DropCampaignsInProgress.FirstOrDefault(x => x.Id == Id);
        
        if (dropsCampaignInProgress is not null)
        {
            var lastTier = dropsCampaignInProgress.TimeBasedDrops.OrderBy(x => x.RequiredMinutesWatched).Last();
            //fixme check if works
            if (lastTier.Self.CurrentMinutesWatched == lastTier.RequiredMinutesWatched)
            {
                return true;
            }
            return false;
        }

        if (TimeBasedDrops?.Any() == true)
        {
            foreach (var timeBasedDrop in TimeBasedDrops)
            {
                foreach (var benefitEdge in timeBasedDrop.BenefitEdges)
                {
                    if (benefitEdge.Benefit.DistributionType == DistributionType.EMOTE)
                    {
                        // Arc raiders emote name is in the time based drops name
                        // BF emote name is in the benefit name
                        // Rust = "EmoteName Emote"
                        List<string> emotes = new List<string>() {timeBasedDrop.Name, benefitEdge.Benefit.Name};
                        var response = await repository.HaveEmote(emotes);
                        return response;
                    }

                    if (benefitEdge.Benefit.DistributionType == DistributionType.BADGE)
                    {
                        List<string> badges = new List<string>() {timeBasedDrop.Name, benefitEdge.Benefit.Name};
                        var response = await repository.HaveBadge(badges);
                        return response;
                    }
                    
                    var correspondingDrop = inventory.GameEventDrops?
                        .FirstOrDefault(x => x.Id == benefitEdge.Benefit.Id);

                    benefitEdge.Benefit.IsClaimed = correspondingDrop != null
                                                    && correspondingDrop.LastAwardedAt >= timeBasedDrop.StartAt
                                                    && correspondingDrop.LastAwardedAt <= timeBasedDrop.EndAt;
                }
            }
        }
        else
        {
            throw new System.Exception("No TimeBasedDrops found in campaign " + Name);
        }


        var allTimeBasedDropsClaimed = TimeBasedDrops.All(drop => drop.IsClaimed());

        return allTimeBasedDropsClaimed;
    }
}