using System.Text.Json.Serialization;
using Discord;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class RewardCampaign : AbstractCampaign
{
    public DistributionType DistributionType { get; set; }
    
    public override bool IsCompleted(Inventory inventory)
    {
        if (inventory.CompletedRewardCampaigns is null)
        {
            return false;
        }

        List<CompletedRewardCampaigns> completedRewardCampaigns = inventory.CompletedRewardCampaigns;

        var anyCompletedRewardCampaigns = completedRewardCampaigns.Any(x => x.Id == Id);

        return anyCompletedRewardCampaigns;
    }
}