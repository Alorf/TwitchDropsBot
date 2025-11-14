using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class DropCampaignSelfEdge
{
    [JsonPropertyName("isAccountConnected")]
    public bool IsAccountConnected { get; set; }
}