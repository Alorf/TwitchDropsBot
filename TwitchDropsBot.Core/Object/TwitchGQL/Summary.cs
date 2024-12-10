namespace TwitchDropsBot.Core.Object.TwitchGQL;

public class Summary
{
    public bool IncludesMWRequirement { get; set; }
    public bool IncludesSubRequirement { get; set; }
    public bool IsSitewide { get; set; }
    public bool IsRewardCampaign { get; set; }
    public bool IsPermanentlyDismissible { get; set; }
}