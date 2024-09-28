namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class RewardCampaignsAvailableToUser : AbstractCampaign
{
    public string? Brand { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? Summary { get; set; }
    public UnlockRequirements? UnlockRequirements { get; set; }
    public List<Reward>? Rewards { get; set; }
}
