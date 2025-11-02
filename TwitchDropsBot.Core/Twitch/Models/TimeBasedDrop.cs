using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TwitchDropsBot.Core.Twitch.Models.Interfaces;

namespace TwitchDropsBot.Core.Twitch.Models;

public partial class TimeBasedDrop
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("requiredSubs")]
    public int RequiredSubs { get; set; }

    [JsonPropertyName("BenefitEdges")]
    public List<DropBenefitEdge> BenefitEdges { get; set; } = new List<DropBenefitEdge>();

    [JsonPropertyName("endAt")]
    public DateTime EndAt { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("requiredMinutesWatched")]
    public int RequiredMinutesWatched { get; set; }

    [JsonPropertyName("startAt")]
    public DateTime StartAt { get; set; }

    [JsonPropertyName("self")]
    public TimeBasedDropSelfEdge? Self { get; set; }
}

public class TimeBasedDropSelfEdge
{
    [JsonPropertyName("hasPreconditionsMet")]
    public bool HasPreconditionsMet { get; set; }
    
    [JsonPropertyName("currentMinutesWatched")]
    public int? CurrentMinutesWatched { get; set; }
    
    [JsonPropertyName("currentSubs")]
    public int? CurrentSubs { get; set; }
    
    [JsonPropertyName("isClaimed")]
    public bool IsClaimed { get; set; }
    
    [JsonPropertyName("DropInstanceID")]
    public string? DropInstanceID { get; set; }
    
}