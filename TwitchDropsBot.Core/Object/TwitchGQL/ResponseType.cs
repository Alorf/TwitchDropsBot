namespace TwitchDropsBot.Core.Object.TwitchGQL;

public class ResponseType
{
    public User User { get; set; }

    public List<RewardCampaignsAvailableToUser> RewardCampaignsAvailableToUser { get; set; }
    
    public User CurrentUser { get; set; }    

    public User Channel { get; set; }    
    
    public Game Game { get; set; }
    
    public StreamPlaybackAccessToken StreamPlaybackAccessToken { get; set; }
    
    public String Token { get; set; }
    
}