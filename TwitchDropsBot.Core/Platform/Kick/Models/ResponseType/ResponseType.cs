using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models.ResponseType;

public class ResponseType<TypeResponse>
{
    [JsonPropertyName("data")]
    public TypeResponse data { get; set; }
    
    [JsonPropertyName("message")]
    public string? message { get; set; }
}