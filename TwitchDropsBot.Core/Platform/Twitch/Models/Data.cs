using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class Data
{
    [JsonPropertyName("currentUser")]
    public User? CurrentUser { get; set; }

    [JsonPropertyName("user")]
    public User? User { get; set; }

    [JsonPropertyName("channel")]
    public User? Channel { get; set; }

    [JsonPropertyName("rewardCampaignsAvailableToUser")]
    public List<RewardCampaign> RewardCampaignsAvailableToUser { get; set; } = new List<RewardCampaign>();

    [JsonPropertyName("game")]
    public Game? Game { get; set; }

    [JsonPropertyName("streamPlaybackAccessToken")]
    public PlaybackAccessToken? StreamPlaybackAccessToken { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}


// public User User { get; set; }
//
// public List<RewardCampaignsAvailableToUser> RewardCampaignsAvailableToUser { get; set; }
//     
// public User CurrentUser { get; set; }    
//
// public User Channel { get; set; }    
//     
// public Game Game { get; set; }
//     
// public StreamPlaybackAccessToken StreamPlaybackAccessToken { get; set; }
//     
// public String Token { get; set; }