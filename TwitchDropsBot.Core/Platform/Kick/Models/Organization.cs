using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;

public class Organization
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
}