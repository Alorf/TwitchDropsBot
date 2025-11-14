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
}

public class CompletedRewardCampaigns
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("startsAt")]
    public DateTime? StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public DateTime? EndsAt { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    [JsonPropertyName("externalURL")]
    public string? ExternalURL { get; set; }

    [JsonPropertyName("aboutURL")]
    public string? AboutURL { get; set; }

    [JsonPropertyName("isSitewide")]
    public bool? IsSitewide { get; set; }

    [JsonPropertyName("game")]
    public Game? Game { get; set; }

    [JsonPropertyName("unlockRequirements")]
    public QuestRewardUnlockRequirements? UnlockRequirements { get; set; }

    [JsonPropertyName("image")]
    public RewardCampaignImageSet? Image { get; set; }

    [JsonPropertyName("rewards")]
    public List<Reward>? Rewards { get; set; }
}