using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class UserDropRewardConnection
{
    [JsonPropertyName("edges")]
    public List<UserDropRewardEdge> Edges { get; set; } = new List<UserDropRewardEdge>();
}