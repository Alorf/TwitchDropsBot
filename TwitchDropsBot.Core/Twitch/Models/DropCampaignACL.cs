using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class DropCampaignACL
{
    [JsonPropertyName("channels")]
    public List<Channel> Channels { get; set; } = new List<Channel>();

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }
}
