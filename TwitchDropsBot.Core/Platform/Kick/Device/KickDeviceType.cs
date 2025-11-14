using System.Net.Http.Headers;

namespace TwitchDropsBot.Core.Platform.Kick.Device;

public class KickDeviceType
{
    public static readonly KickDevice MOBILE = new KickDevice(
        "f3a7c8b1e5d9246aa8f6b37d5c8e9a2fd4e1c0abf79d3826b4c5e7a9d8f2b6c3",
        new List<string>
        {
            "KickMobile/40.9.2 (com.kick.mobile; platform: android; build:60006703)",
            "okhttp/4.12.0"
        },
        new Dictionary<string, string>
        {
            {"x-app-version", "40.9.2"},
            {"x-kick-app", "mobile"},
        }
        
    );
    
    public static readonly KickDevice WEB = new KickDevice(
        "e1393935a959b4020a4491574f6490129f678acdaa92760471263db43487f823",
        new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36"
        }
    );
}