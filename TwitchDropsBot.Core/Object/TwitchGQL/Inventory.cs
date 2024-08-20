namespace TwitchDropsBot.Core.Object;

public class Inventory
{
    public List<DropCampaign>? DropCampaignsInProgress { get; set; }
    public List<GameEventDrop>? GameEventDrops { get; set; }
}