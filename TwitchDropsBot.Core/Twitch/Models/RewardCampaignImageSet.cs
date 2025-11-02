using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class RewardCampaignImageSet
{
    [JsonPropertyName("image1xURL")]
    public string? Image1xURL { get; set; }
}