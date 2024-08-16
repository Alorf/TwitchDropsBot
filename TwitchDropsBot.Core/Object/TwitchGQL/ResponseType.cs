namespace TwitchDropsBot.Core.Object;

public class ResponseType
{
    public User User { get; set; }
    
    public User CurrentUser { get; set; }    
    
    public Game Game { get; set; }
    
    public StreamPlaybackAccessToken StreamPlaybackAccessToken { get; set; }
    
    public String Token { get; set; }
    
}