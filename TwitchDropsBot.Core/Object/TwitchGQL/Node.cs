namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class Node
{
    public string Id { get; set; }
    public int ViewersCount { get; set; }
    public Broadcaster Broadcaster { get; set; }

    public string Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ThumbnailURL { get; set; }
    public List<Action> Actions { get; set; }
}