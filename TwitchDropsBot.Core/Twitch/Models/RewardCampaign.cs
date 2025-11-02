using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TwitchDropsBot.Core.Twitch.Models.Custom;

namespace TwitchDropsBot.Core.Twitch.Models;

public partial class RewardCampaign : AbstractCampaign
{
    // [JsonPropertyName("id")]
    // public string? Id { get; set; }

    // [JsonPropertyName("name")]
    // public string? Name { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("startsAt")]
    public DateTime? StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public DateTime? EndsAt { get; set; }

    // [JsonPropertyName("status")]
    // public string? Status { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("instruction")]
    public string? Instruction { get; set; }

    [JsonPropertyName("externalURL")]
    public string? ExternalURL { get; set; }

    [JsonPropertyName("rewardValueURLParam")]
    public string? RewardValueURLParam { get; set; }

    [JsonPropertyName("aboutURL")]
    public string? AboutURL { get; set; }

    [JsonPropertyName("isSiteWide")]
    public bool IsSiteWide { get; set; }

    // [JsonPropertyName("game")]
    // public Game? Game { get; set; }

    [JsonPropertyName("unlockRequirements")]
    public QuestRewardUnlockRequirements? UnlockRequirements { get; set; }

    [JsonPropertyName("image")]
    public RewardCampaignImageSet? Image { get; set; }

    [JsonPropertyName("rewards")]
    public List<Reward> Rewards { get; set; } = new List<Reward>();
}