using System;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class RewardCampaignCode
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("rewardID")]
    public string? RewardID { get; set; }

    [JsonPropertyName("rewardCampaignID")]
    public string? RewardCampaignID { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }
}