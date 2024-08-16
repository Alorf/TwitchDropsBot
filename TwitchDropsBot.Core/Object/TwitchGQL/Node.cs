namespace TwitchDropsBot.Core.Object;

public class Node
{
    public string Id { get; set; }
    public int ViewersCount { get; set; }
    public Broadcaster Broadcaster { get; set; }
}