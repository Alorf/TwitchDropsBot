using TwitchDropsBot.Core;

using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Twitch.Services;
using TwitchDropsBot.Core.Platform.Twitch.Settings;

namespace TwitchDropsBot.Console.Platform;

public class Twitch
{
    public static async Task AuthTwitchDeviceAsync()
    {
        var jsonResponse = await TwitchAuthService.GetCodeAsync();
        var deviceCode = jsonResponse.RootElement.GetProperty("device_code").GetString();
        var userCode = jsonResponse.RootElement.GetProperty("user_code").GetString();
        var verificationUri = jsonResponse.RootElement.GetProperty("verification_uri").GetString();

        SystemLoggerService.Logger.Information($"Please go to {verificationUri} and enter the code: {userCode}");

        if (deviceCode is null)
        {
            SystemLoggerService.Logger.Error("Failed to get device code.");
            Environment.Exit(1);
        }

        jsonResponse = await TwitchAuthService.CodeConfirmationAsync(deviceCode);

        if (jsonResponse == null)
        {
            SystemLoggerService.Logger.Error("Failed to authenticate the user.");
            Environment.Exit(1);
        }

        var secret = jsonResponse.RootElement.GetProperty("access_token").GetString();

        if (secret is null)
        {
            SystemLoggerService.Logger.Error("Failed to get secret.");
            Environment.Exit(1);
        }

        TwitchUserSettings user = await TwitchAuthService.ClientSecretUserAsync(secret);

        // Save the user into config.json
        AppSettingsService.Settings.TwitchSettings.TwitchUsers.RemoveAll(x => x.Id == user.Id);
        AppSettingsService.Settings.TwitchSettings.TwitchUsers.Add((TwitchUserSettings)user);
        AppSettingsService.SaveConfig();
    }
}