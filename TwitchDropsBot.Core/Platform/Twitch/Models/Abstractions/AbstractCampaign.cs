using System.Text.Json.Serialization;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Repository;

namespace TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;

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
    
    [JsonPropertyName("startAt")]
    public DateTime StartAt { get; set; }

    [JsonPropertyName("endAt")]
    public DateTime? EndAt { get; set; }
    
    public abstract Task<bool> IsCompleted(Inventory inventory, TwitchGqlRepository _repository);
    public TimeBasedDrop? FindTimeBasedDrop(string dropId)
    {
        return TimeBasedDrops.FirstOrDefault(drop => drop.Id == dropId);
    }

    protected bool Equals(AbstractCampaign other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((AbstractCampaign)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}