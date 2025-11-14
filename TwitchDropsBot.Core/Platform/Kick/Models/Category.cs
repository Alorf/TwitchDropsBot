
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;

public partial class Category
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
        
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; }
        
    [JsonPropertyName("name")]
    public string Name { get; set; }
        
    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}