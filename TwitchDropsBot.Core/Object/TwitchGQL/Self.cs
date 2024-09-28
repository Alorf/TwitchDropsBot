namespace TwitchDropsBot.Core.Object.TwitchGQL;

public class Self
{
    public bool IsAccountConnected { get; set; }
    public bool HasPreconditionsMet { get; set; }
    public int CurrentMinutesWatched { get; set; }
    public bool IsClaimed { get; set; }
    public string DropInstanceID { get; set; }
}