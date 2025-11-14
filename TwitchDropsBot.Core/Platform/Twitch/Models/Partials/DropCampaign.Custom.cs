
using System.Text.Json.Serialization;
using Discord;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class DropCampaign : AbstractCampaign
{

    public override bool IsCompleted(Inventory inventory)
    {
        if (inventory.DropCampaignsInProgress.Any(x => x.Id == Id))
        {
            return false;
        }

        if (TimeBasedDrops?.Any() == true)
        {
            foreach (var timeBasedDrop in TimeBasedDrops)
            {
                foreach (var benefitEdge in timeBasedDrop.BenefitEdges)
                {
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