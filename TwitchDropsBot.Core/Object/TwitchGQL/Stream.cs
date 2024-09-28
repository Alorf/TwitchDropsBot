namespace TwitchDropsBot.Core.Object.TwitchGQL;

public class Stream
{
    public string Id { get; set; }
    public int ViewersCount { get; set; }
    public List<Edge> Edges { get; set; }
}