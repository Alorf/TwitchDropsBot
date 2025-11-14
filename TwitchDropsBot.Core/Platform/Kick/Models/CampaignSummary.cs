using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;

public class CampaignSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("progress_units")]
    public double ProgressUnits { get; set; }

    [JsonPropertyName("rewards")]
    public List<Reward> Rewards { get; set; }
}
