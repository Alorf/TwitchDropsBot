using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public class QuestRewardUnlockRequirements
{
    [JsonPropertyName("subsGoal")]
    public int SubsGoal  { get; set; }
    
    [JsonPropertyName("minuteWatchedGoal")]
    public int MinuteWatchedGoal  { get; set; }
}