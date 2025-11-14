using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class Game
{
    [JsonIgnore]
    public bool IsFavorite { get; set; }
}