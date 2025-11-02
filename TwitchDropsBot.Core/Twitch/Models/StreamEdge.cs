using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class StreamEdge
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("node")]
    public Stream? Node { get; set; }
}