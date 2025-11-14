namespace TwitchDropsBot.Core.Platform.Twitch.Device;

public class TwitchDeviceType
{
    public static readonly TwitchDevice WEB = new TwitchDevice(
        "https://www.twitch.tv",
        "kimne78kx3ncx6brgo4mv6wki5h1ko",
        new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.3"
        }
    );

    public static readonly TwitchDevice MOBILE_WEB = new TwitchDevice(
        "https://m.twitch.tv",
        "r8s4dac0uhzifbpu9sjdiwzctle17ff",
        new List<string>
        {
            "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 13; SM-A205U) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 13; SM-A102U) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 13; SM-G960U) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 13; SM-N960U) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 13; LM-Q720) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 13; LM-X420) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Mobile Safari/537.36"
        }
    );

    public static readonly TwitchDevice ANDROID_APP = new TwitchDevice(
        "https://www.twitch.tv",
        "kd1unb4b3q4t58fwlpcbzcbnm76a8fp",
        new List<string>
        {
            "Dalvik/2.1.0 (Linux; U; Android 15; SM-G977N Build/BP1A.250505.005)"
        }
    );

    public static readonly TwitchDevice SMARTBOX = new TwitchDevice(
        "https://android.tv.twitch.tv",
        "ue6666qo983tsx6so1t0vnawi233wa",
        new List<string>
        {
            "Mozilla/5.0 (Linux; Android 7.1; Smart Box C1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36"
        }
    );
}