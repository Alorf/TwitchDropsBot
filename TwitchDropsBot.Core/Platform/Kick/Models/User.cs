using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;

public class User
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("profile_picture")]
    public string ProfilePicture { get; set; }
    
    [JsonPropertyName("username")]
    public string Username { get; set; }
}