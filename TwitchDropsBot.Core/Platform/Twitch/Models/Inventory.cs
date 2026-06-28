using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

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
    
    [JsonPropertyName("earnedDropRewards")]
    public EarnedDropRewardConnection EarnedDropRewards { get; set; }
}

public class EarnedDropRewardConnection
{
    [JsonPropertyName("edges")]
    public List<EarnedDropRewardEdge> Edges { get; set; }
}

public class EarnedDropRewardEdge
{
    [JsonPropertyName("node")]
    public EarnedDropReward Node { get; set; }
}

public class EarnedDropReward
{
    [JsonPropertyName("id")]
    public string Id { get; set; } // Node ID or item ID will be the item to claim
    
    [JsonPropertyName("item")]
    public DropsReward Item { get; set; }
    
    [JsonPropertyName("campaign")]
    public DropsCampaign Campaign { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; }
}