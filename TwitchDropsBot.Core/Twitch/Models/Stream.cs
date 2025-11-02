using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class Stream
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("viewersCount")]
    public int ViewersCount { get; set; }

    [JsonPropertyName("previewImageURL")]
    public string? PreviewImageURL { get; set; }

    [JsonPropertyName("broadcaster")]
    public User? Broadcaster { get; set; }

    [JsonPropertyName("freeFormTags")]
    public List<FreeFormTag> FreeFormTags { get; set; } = new List<FreeFormTag>();

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("game")]
    public Game? Game { get; set; }

    [JsonPropertyName("previewThumbnailProperties")]
    public PreviewThumbnailProperties? PreviewThumbnailProperties { get; set; }
}