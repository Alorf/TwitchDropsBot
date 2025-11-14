using System.Net;
using System.Web;
using Serilog;
using TwitchDropsBot.Console.Utils;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Platform.Kick.Services;
using TwitchDropsBot.Core.Platform.Kick.Settings;
using TwitchDropsBot.Core.Platform.Shared.Services;

namespace TwitchDropsBot.Console.Platform;

public class Kick
{
    
    public static async Task AuthKickDeviceAsync(ILogger logger)
    {
        var (guid, code, url) = KickAuthService.CreateLoginUrl();

        logger.Information(url);
        
        var PollService = new KickAuthPollService();

        var token = await PollService.PollAuthenticateAsync(guid, code);

        var (id, username) = await PollService.GetUserInfo(token);

        //Request to /me to retrieve user information
        var config = AppSettingsService.Settings;

        var userConfig = new KickUserSettings();
        userConfig.Login = username;
        userConfig.Id = id;
        userConfig.BearerToken = token;
        userConfig.Enabled = true;

        config.KickSettings.KickUsers.RemoveAll(x => x.Id == userConfig.Id);
        config.KickSettings.KickUsers.Add(userConfig);
        AppSettingsService.SaveConfig();
    }
}