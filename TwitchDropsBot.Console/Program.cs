using System.Text.Json;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Utilities;

AppConfig config = AppConfig.GetConfig();

while (config.Users.Count == 0)
{
    SystemLogger.Info("No users found in the configuration file.");
    SystemLogger.Info("Login process will start.");

    await AuthDeviceAsync();
    config = AppConfig.GetConfig();
}

foreach (ConfigUser user in config.Users)
{
    TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId);

    Bot bot = new Bot(twitchUser);
    while (true)
    {
        try
        {
            await bot.StartAsync();
        }
        catch (NoBroadcasterOrNoCampaignFound ex)
        {
            twitchUser.Logger.Info(ex.Message);
            twitchUser.Logger.Info("Waiting for 5 minutes before trying again.");
        }
        catch (Exception ex)
        {
            twitchUser.Logger.Error(ex);
        }

        await Task.Delay(300000);
    }
}

static async Task AuthDeviceAsync()
{
    var jsonResponse = await AuthSystem.GetCodeAsync();
    var deviceCode = jsonResponse.RootElement.GetProperty("device_code").GetString();
    var userCode = jsonResponse.RootElement.GetProperty("user_code").GetString();
    var verificationUri = jsonResponse.RootElement.GetProperty("verification_uri").GetString();

    SystemLogger.Info($"Please go to {verificationUri} and enter the code: {userCode}");

    jsonResponse = await AuthSystem.CodeConfirmationAsync(deviceCode);

    if (jsonResponse == null)
    {
        SystemLogger.Error("Failed to authenticate the user.");
        Environment.Exit(1);
    }

    var secret = jsonResponse.RootElement.GetProperty("access_token").GetString();

    ConfigUser user = await AuthSystem.ClientSecretUserAsync(secret);

    // Save the user into config.json
    var config = AppConfig.GetConfig();
    config.Users.RemoveAll(x => x.Id == user.Id);
    config.Users.Add(user);

    AppConfig.SaveConfig(config);
}