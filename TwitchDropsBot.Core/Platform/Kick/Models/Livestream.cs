using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;

public class Livestream
{
    [JsonPropertyName("categories")]
    public List<Category> Category { get; set; }
    
    [JsonPropertyName("channel")]
    public Channel Channel { get; set; }
    
    // fixme
    [JsonPropertyName("id")]
    public dynamic Id { get; set; }
    
    [JsonPropertyName("is_mature")]
    public bool IsMature { get; set; }
    
    [JsonPropertyName("language")]
    public string Language { get; set; }
    
    [JsonPropertyName("start_time")]
    public string StartTime { get; set; }
    
    [JsonPropertyName("tags")]
    public List<string> tags { get; set; }
    
    [JsonPropertyName("thumbnail")]
    public Thumbnail Thumbnail { get; set; }
    
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("viewer_count")]
    public int ViewerCount { get; set; }
}

public class Thumbnail
{
    [JsonPropertyName("src")]
    public string Src { get; set; }
    
    [JsonPropertyName("srcset")]
    public string Srcset { get; set; }
}