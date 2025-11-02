using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class PreviewThumbnailProperties
{
    [JsonPropertyName("blurReason")]
    public string? BlurReason { get; set; }
}