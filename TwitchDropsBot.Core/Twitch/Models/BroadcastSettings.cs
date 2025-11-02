using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class BroadcastSettings
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("game")]
    public Game? Game { get; set; }
}