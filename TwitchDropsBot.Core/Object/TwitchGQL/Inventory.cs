namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class Inventory
{
    public List<DropCampaign>? DropCampaignsInProgress { get; set; }
    public List<GameEventDrop>? GameEventDrops { get; set; }
}