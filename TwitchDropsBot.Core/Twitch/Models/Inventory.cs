using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class Inventory
{
    [JsonPropertyName("rewardValue")]
    public RewardCampaignCode? RewardValue { get; set; }
    
    [JsonPropertyName("dropCampaignsInProgress")]
    public List<DropCampaign> DropCampaignsInProgress { get; set; } = new List<DropCampaign>();

    [JsonPropertyName("gameEventDrops")]
    public List<UserDropReward> GameEventDrops { get; set; } = new List<UserDropReward>();
    

    // Todo : Verify the correct name of class 
    [JsonPropertyName("completedRewardCampaigns")]
    public List<CompletedRewardCampaigns> CompletedRewardCampaigns { get; set; } = new List<CompletedRewardCampaigns>();
}

public class CompletedRewardCampaigns
{
    public string? id { get; set; }
    public string? name { get; set; }
    public List<Reward>? rewards { get; set; }
}