using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class DropBenefit
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("entitlementLimit")]
    public int EntitlementLimit { get; set; }

    [JsonPropertyName("game")]
    public Game? Game { get; set; }

    [JsonPropertyName("imageAssetURL")]
    public string? ImageAssetURL { get; set; }

    [JsonPropertyName("isIosAvailable")]
    public bool IsIosAvailable { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ownerOrganization")]
    public Organization? OwnerOrganization { get; set; }

    [JsonPropertyName("distributionType")]
    public DistributionType? DistributionType { get; set; }
}