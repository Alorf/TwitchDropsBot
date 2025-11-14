using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;

public class Reward
{
    [JsonPropertyName("category_id")]
    public int CategoryId { get; set; }
    
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("organization_id")]
    public string organizationId { get; set; }
    
    [JsonPropertyName("required_units")]
    public int RequiredUnits { get; set; }
    
    [JsonPropertyName("claimed")]
    public bool Claimed { get; set; }
    
    [JsonPropertyName("progress")]
    public double Progress { get; set; }
    
}