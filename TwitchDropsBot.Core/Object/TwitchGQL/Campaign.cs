namespace TwitchDropsBot.Core.Object;

public class Campaign
{
    public string Id { get; set; }
    public string DetailsURL { get; set; }
    public string AccountLinkURL { get; set; }
    public Self Self { get; set; }
}