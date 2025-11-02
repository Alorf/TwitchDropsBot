using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class StreamConnection
{
    [JsonPropertyName("edges")]
    public List<StreamEdge> Edges { get; set; } = new List<StreamEdge>();
}