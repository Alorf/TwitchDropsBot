namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class GameEventDrop
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Game? Game { get; set; }
    public string? ImageURL { get; set; }
    public bool IsConnected { get; set; }
    public int? TotalCount { get; set; }
    public DateTime lastAwardedAt { get; set; }

}