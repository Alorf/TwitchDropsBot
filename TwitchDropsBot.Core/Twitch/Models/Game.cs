using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public partial class Game
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("boxArtUrl")]
    public string? BoxArtUrl { get; set; }

    [JsonPropertyName("streams")]
    public StreamConnection? Streams { get; set; }
}
