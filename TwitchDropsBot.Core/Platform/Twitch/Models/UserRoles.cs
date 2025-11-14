using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class UserRoles
{
    [JsonPropertyName("isPartner")]
    public bool IsPartner { get; set; }
}