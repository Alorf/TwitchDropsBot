using System.Text.Json.Serialization;
using TwitchDropsBot.Core.Twitch.Models;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class BroadcastSettings
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("game")]
    public Game? Game { get; set; }
}