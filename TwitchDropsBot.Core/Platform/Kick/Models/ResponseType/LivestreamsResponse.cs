using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models.ResponseType;

public class LivestreamsResponse
{
    [JsonPropertyName("livestreams")]
    public List<Livestream> livestreams { get; set; }
}