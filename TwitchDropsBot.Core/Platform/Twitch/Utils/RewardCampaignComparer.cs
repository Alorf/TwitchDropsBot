using TwitchDropsBot.Core.Platform.Twitch.Models;

namespace TwitchDropsBot.Core.Platform.Twitch.Utils;

public class RewardCampaignComparer : IEqualityComparer<CompletedRewardCampaigns>
{
    public bool Equals(CompletedRewardCampaigns x, CompletedRewardCampaigns y)
    {
        if (x == null || y == null) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode(CompletedRewardCampaigns obj)
    {
        return obj.Id.GetHashCode();
    }
}
