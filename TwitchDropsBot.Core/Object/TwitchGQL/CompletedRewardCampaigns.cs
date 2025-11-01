namespace TwitchDropsBot.Core.Object.TwitchGQL;

public class CompletedRewardCampaigns
{
    public string? id { get; set; }
    public string? name { get; set; }
    public List<Reward>? rewards { get; set; }
}