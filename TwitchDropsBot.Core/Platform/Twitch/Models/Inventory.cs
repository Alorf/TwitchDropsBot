using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class Inventory
{
    [JsonPropertyName("dropCampaignsInProgress")]
    public List<DropCampaign> DropCampaignsInProgress { get; set; } = new List<DropCampaign>();

    [JsonPropertyName("gameEventDropsConnection")]
    public UserDropRewardConnection GameEventDropsConnection { get; set; } = new UserDropRewardConnection();

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