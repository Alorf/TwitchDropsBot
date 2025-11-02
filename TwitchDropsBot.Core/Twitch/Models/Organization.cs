using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class Organization
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}