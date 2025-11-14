using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class DropCampaignSummary
{
    [JsonPropertyName("includesMWRequirement")]
    public bool IncludesMwRequirement { get; set; }

    [JsonPropertyName("includesSubRequirement")]
    public bool IncludesSubRequirement { get; set; }

    [JsonPropertyName("isSitewide")]
    public bool IsSitewide { get; set; }

    [JsonPropertyName("isRewardCampaign")]
    public bool IsRewardCampaign { get; set; }

    [JsonPropertyName("isPermanentlyDismissible")]
    public bool IsPermanentlyDismissible { get; set; }
}