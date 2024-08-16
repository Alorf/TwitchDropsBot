namespace TwitchDropsBot.Core.Object;

public class DropCampaign
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Game? Game { get; set; }
    public string Status { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public Self Self { get; set; }
    public Allow Allow { get; set; }
    public List<TimeBasedDrop> TimeBasedDrops { get; set; }
    
    public List<Channel>? GetChannels()
    {
        return Allow?.Channels;
    }
}