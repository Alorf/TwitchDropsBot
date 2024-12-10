namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class DropCurrentSession
{
    public string? DropId { get; set; }
    public Channel Channel { get; set; }
    public Game Game { get; set; }
    public int CurrentMinutesWatched { get; set; }
    public int requiredMinutesWatched { get; set; }

    public override string ToString()
    {
        return $"{Channel.Name}({Channel.Id}) - {DropId}";
    }
}