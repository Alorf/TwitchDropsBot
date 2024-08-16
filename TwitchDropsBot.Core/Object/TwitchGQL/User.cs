namespace TwitchDropsBot.Core.Object;

public class User : AbstractBroadcaster
{
    public string displayName { get; set; }
    
    // if stream is null => user is offline
    public Stream? Stream { get; set; }
    
    public BroadcastSettings BroadcastSettings { get; set; }
    public List<DropCampaign> DropCampaigns { get; set; }
    
    public DropCurrentSession DropCurrentSession { get; set; }
        
    public Inventory Inventory { get; set; }

    public bool IsLive()
    {
        return this.Stream != null;
    }
}