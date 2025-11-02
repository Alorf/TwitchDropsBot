using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class UserRoles
{
    [JsonPropertyName("isPartner")]
    public bool IsPartner { get; set; }
}