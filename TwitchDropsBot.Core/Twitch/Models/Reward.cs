using System;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class Reward // Father Type RewardCampaign
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("BannerImage")]
    public RewardCampaignImageSet? BannerImage { get; set; }

    [JsonPropertyName("thumbnailImage")]
    public RewardCampaignImageSet? ThumbnailImage { get; set; }

    [JsonPropertyName("earnableUntil")]
    public DateTime? EarnableUntil { get; set; }

    [JsonPropertyName("redemptionInstructions")]
    public string? RedemptionInstructions { get; set; }

    [JsonPropertyName("redemptionURL")]
    public string? RedemptionURL { get; set; }
}