using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class RewardCampaignImageSet
{
    [JsonPropertyName("image1xURL")]
    public string? Image1xURL { get; set; }
}