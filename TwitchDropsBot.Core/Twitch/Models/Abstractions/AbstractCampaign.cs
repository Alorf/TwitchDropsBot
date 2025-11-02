using System.Text.Json.Serialization;
using TwitchDropsBot.Core.Object;

namespace TwitchDropsBot.Core.Twitch.Models.Custom;

public abstract class AbstractCampaign
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("game")]
    public Game? Game { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("timeBasedDrops")]
    public List<TimeBasedDrop> TimeBasedDrops { get; set; } = new List<TimeBasedDrop>();
    
    [JsonPropertyName("allow")]
    public DropCampaignACL Allow { get; set; }

    public abstract Task NotifiateAsync(TwitchUser twitchUser);
    
    public abstract bool IsCompleted(Inventory inventory);
    public TimeBasedDrop? FindTimeBasedDrop(string dropId)
    {
        return TimeBasedDrops.FirstOrDefault(drop => drop.Id == dropId);
    }
}