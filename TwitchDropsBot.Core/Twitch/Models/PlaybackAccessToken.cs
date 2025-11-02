using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public class PlaybackAccessToken
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("Authorization")]
    public PlaybackAccessTokenAuthorization? Authorization { get; set; }
}
