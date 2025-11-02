using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TwitchDropsBot.Core.Twitch.Models.Custom;

namespace TwitchDropsBot.Core.Twitch.Models;

public partial class DropCampaign : AbstractCampaign
{
    // [JsonPropertyName("id")]
    // public string? Id { get; set; }

    // [JsonPropertyName("name")]
    // public string? Name { get; set; }

    [JsonPropertyName("owner")]
    public Organization? Owner { get; set; }

    // [JsonPropertyName("game")]
    // public Game? Game { get; set; }

    // [JsonPropertyName("status")]
    // public string? Status { get; set; }

    [JsonPropertyName("startAt")]
    public DateTime StartAt { get; set; }

    [JsonPropertyName("endAt")]
    public DateTime? EndAt { get; set; }

    [JsonPropertyName("detailsURL")]
    public string? DetailsURL { get; set; }

    [JsonPropertyName("accountLinkURL")]
    public string? AccountLinkURL { get; set; }

    [JsonPropertyName("self")]
    public DropCampaignSelfEdge? Self { get; set; }

    // [JsonPropertyName("allow")]
    // public DropCampaignACL? Allow { get; set; }

    // [JsonPropertyName("timeBasedDrops")]
    // public List<TimeBasedDrop> TimeBasedDrops { get; set; } = new List<TimeBasedDrop>();

    [JsonPropertyName("summary")]
    public DropCampaignSummary? Summary { get; set; }
}