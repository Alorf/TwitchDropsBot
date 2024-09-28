namespace TwitchDropsBot.Core.Object.TwitchGQL;

public class TimeBasedDrop
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int RequiredMinutesWatched { get; set; }
    public Self Self { get; set; }
    public Campaign Campaign { get; set; }
}