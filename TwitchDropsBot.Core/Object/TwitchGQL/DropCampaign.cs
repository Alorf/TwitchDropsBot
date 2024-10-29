namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class DropCampaign : AbstractCampaign
{
    public string ImageURL { get; set; }
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