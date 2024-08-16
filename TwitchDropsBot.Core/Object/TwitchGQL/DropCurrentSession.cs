using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Object;

public class DropCurrentSession
{
    public string? DropId { get; set; }
    public Channel Channel { get; set; }
    public Game Game { get; set; }
    public int CurrentMinutesWatched { get; set; }
    public int requiredMinutesWatched { get; set; }
    
    
}