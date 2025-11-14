using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;

public class Channel
{
    [JsonPropertyName("banner_picture_url")]
    public string BannerPictureUrl { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("slug")]
    public string slug { get; set; }
    
    [JsonPropertyName("user")]
    public User user { get; set; }
    
    [JsonPropertyName("livestream")]
    public Livestream Livestream { get; set; }
}