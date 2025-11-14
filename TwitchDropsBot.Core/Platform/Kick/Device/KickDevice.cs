using System.Net.Http.Headers;

namespace TwitchDropsBot.Core.Platform.Kick.Device;

public class KickDevice
{
    public string ClientToken { get; }
    public List<String> UserAgents { get; }
    public Dictionary<string, string>? Headers { get; }
    
    public KickDevice(string clientToken, List<string> userAgents, Dictionary<string, string>? headers = null)
    {
        ClientToken= clientToken;
        UserAgents = userAgents;
        if (headers is not null)
        {
            Headers = headers;    
        }
    }
}