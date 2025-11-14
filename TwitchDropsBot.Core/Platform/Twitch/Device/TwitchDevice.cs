namespace TwitchDropsBot.Core.Platform.Twitch.Device;

public class TwitchDevice
{
    public string URL { get; }
    public string ClientID { get; }
    public List<String> UserAgents { get; }
    
    public TwitchDevice(string url, string clientID, List<string> userAgents)
    {
        URL = url;
        ClientID = clientID;
        UserAgents = userAgents;
    }
}