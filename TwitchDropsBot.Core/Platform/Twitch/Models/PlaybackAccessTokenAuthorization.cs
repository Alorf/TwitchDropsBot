using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class PlaybackAccessTokenAuthorization
{
    [JsonPropertyName("isForbidden")]
    public bool IsForbidden { get; set; }

    [JsonPropertyName("forbiddenReasonCode")]
    public string? ForbiddenReasonCode { get; set; }
}