using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class DropCurrentSession
{
    [JsonPropertyName("channel")]
    public Channel? Channel { get; set; }

    [JsonPropertyName("game")]
    public Game? Game { get; set; }

    [JsonPropertyName("currentMinutesWatched")]
    public int CurrentMinutesWatched { get; set; }

    [JsonPropertyName("requiredMinutesWatched")]
    public int RequiredMinutesWatched { get; set; }

    [JsonPropertyName("dropID")]
    public string? DropId { get; set; }
}
