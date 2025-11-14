namespace TwitchDropsBot.Core.Platform.Kick.Models;

public partial class Campaign
{
    public bool IsCompleted()
    {
        // todo : to verify
        if (Status == "in_progress")
        {
            return false;
        }

        if (Rewards?.Any() != true)
        {
            throw new System.Exception("No Rewards found in campaign " + Name);
        }


        var allTimeBasedDropsClaimed = Rewards.All(reward => reward.Claimed);

        return allTimeBasedDropsClaimed;
    }
}