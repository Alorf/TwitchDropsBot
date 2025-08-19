using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Utilities;

AppConfig config = AppConfig.Instance;

if (args.Length > 0 && args[0] == "--add-account" && config.Users.Count != 0)
{
    do
    {
        SystemLogger.Info("Do you want to add another account? (Y/N)");
        string answer = Console.ReadLine();
        if (answer.ToLower() == "n")
        {
            break;
        }

        await AuthDeviceAsync();
    } while (true);
}

while (config.Users.Count == 0)
{
    SystemLogger.Info("No users found in the configuration file.");
    SystemLogger.Info("Login process will start.");

    await AuthDeviceAsync();
}

TimeSpan waitingTime;
List<Task> botTasks = new List<Task>();

foreach (ConfigUser user in config.Users)
{
    TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId, user.FavouriteGames);
    twitchUser.DiscordWebhookURl = config.WebhookURL;
    
    if (!user.Enabled)
    {
        SystemLogger.Info($"User {twitchUser.Login} is not enabled, skipping...");
        continue;
    }
    
    botTasks.Add(Bot.StartBot(twitchUser));
}

await Task.WhenAll(botTasks);
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
    var config = AppConfig.Instance;
    config.Users.RemoveAll(x => x.Id == user.Id);
    config.Users.Add(user);

    config.SaveConfig();
}