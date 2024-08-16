namespace TwitchDropsBot.Core.Object;

public class TwitchClient
{
    public string URL { get; }
    public string ClientID { get; }
    public List<String> UserAgents { get; }
    
    public TwitchClient(string url, string clientID, List<string> userAgents)
    {
        URL = url;
        ClientID = clientID;
        UserAgents = userAgents;
    }
}