using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Kick.Services;
using TwitchDropsBot.Core.Platform.Kick.Settings;
using TwitchDropsBot.Core.Platform.Shared.Services;

namespace TwitchDropsBot.Console.Platform;

public class Kick
{
    
    public static async Task AuthKickDeviceAsync(ILogger logger, SettingsManager manager)
    {
        var (guid, code, url) = KickAuthService.CreateLoginUrl();

        logger.LogInformation("Please, open this link with the Kick mobile app");
        logger.LogInformation(url);
        
        var PollService = new KickAuthPollService();

        var token = await PollService.PollAuthenticateAsync(logger, guid, code);

        if (token is null)
        {
            logger.LogInformation("Failed to authenticate");
            return;
        }

        var (id, username) = await PollService.GetUserInfo(token);

        var settings = manager.Read();

        //Request to /me to retrieve user information

        var userConfig = new KickUserSettings();
        userConfig.Login = username;
        userConfig.Id = id;
        userConfig.BearerToken = token;
        userConfig.Enabled = true;

        settings.KickSettings.KickUsers.RemoveAll(x => x.Id == userConfig.Id);
        settings.KickSettings.KickUsers.Add(userConfig);
        manager.Save(settings);
    }
}