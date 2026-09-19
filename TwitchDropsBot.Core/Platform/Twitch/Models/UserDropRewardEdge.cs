using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class UserDropRewardEdge
{
    [JsonPropertyName("node")]
    public UserDropReward Node { get; set; }
}