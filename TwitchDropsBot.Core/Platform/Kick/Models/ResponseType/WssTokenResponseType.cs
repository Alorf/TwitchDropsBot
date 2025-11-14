using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models.ResponseType;

public class WssTokenResponseType
{
    [JsonPropertyName("token")]
    public string Token { get; set; }
}