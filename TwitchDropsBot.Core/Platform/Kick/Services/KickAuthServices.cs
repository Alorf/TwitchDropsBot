using System;
using System.Security.Cryptography;

namespace TwitchDropsBot.Core.Platform.Kick.Services;

public static class KickAuthService
{
    private const string BaseLoginUrl = "https://kick.com/tv/login";

    public static (string uuid, string code, string url) CreateLoginUrl()
    {
        string uuid = Guid.NewGuid().ToString().ToUpperInvariant();
        int number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        string code = number.ToString("D6");
        string url = $"{BaseLoginUrl}?uuid={uuid}&code={code}";
        
        return (uuid, code, url);
    }
}