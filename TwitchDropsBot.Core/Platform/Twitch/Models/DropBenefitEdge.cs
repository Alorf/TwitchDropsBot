using System;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class DropBenefitEdge
{
    [JsonPropertyName("benefit")]
    public DropBenefit? Benefit { get; set; }
    
    [JsonPropertyName("entitlementLimit")]
    public int EntitlementLimit { get; set; }
}