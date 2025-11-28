using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core;

using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Twitch.Services;
using TwitchDropsBot.Core.Platform.Twitch.Settings;

namespace TwitchDropsBot.Console.Platform;

public class Twitch
{
    public static async Task AuthTwitchDeviceAsync(SettingsManager manager, ILogger logger)
    {
        var jsonResponse = await TwitchAuthService.GetCodeAsync();
        var deviceCode = jsonResponse.RootElement.GetProperty("device_code").GetString();
        var userCode = jsonResponse.RootElement.GetProperty("user_code").GetString();
        var verificationUri = jsonResponse.RootElement.GetProperty("verification_uri").GetString();

        logger.LogInformation($"Please go to {verificationUri} and enter the code: {userCode}");

        if (deviceCode is null)
        {
            logger.LogError("Failed to get device code.");
            Environment.Exit(1);
        }

        jsonResponse = await TwitchAuthService.CodeConfirmationAsync(deviceCode, logger);

        if (jsonResponse == null)
        {
            logger.LogError("Failed to authenticate the user.");
            Environment.Exit(1);
        }

        var secret = jsonResponse.RootElement.GetProperty("access_token").GetString();

        if (secret is null)
        {
            logger.LogError("Failed to get secret.");
            Environment.Exit(1);
        }

        TwitchUserSettings user = await TwitchAuthService.ClientSecretUserAsync(secret);

        var settings = manager.Read();
        // Save the user into config.json
        settings.TwitchSettings.TwitchUsers.RemoveAll(x => x.Id == user.Id);
        settings.TwitchSettings.TwitchUsers.Add(user);
        manager.Save(settings);
    }
}