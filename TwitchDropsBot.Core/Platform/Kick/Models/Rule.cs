using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;

public class Rule
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
}