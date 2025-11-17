using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchDropsBot.Console.Platform;
using TwitchDropsBot.Console.Utils;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Kick.Services;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Twitch;
using TwitchDropsBot.Core.Platform.Twitch.Bot;

var services = AppService.Init();

var serviceCollection = services.BuildServiceProvider();

var logger = serviceCollection.GetService<ILogger>();
var config = AppSettingsService.Settings; 

if (config is null)
{
    Console.WriteLine("Config is null");
    return;
}
if (logger is null)
{
    Console.WriteLine("Logger is null");
    return;
}

var addAccountEnv = Environment.GetEnvironmentVariable("ADD_ACCOUNT");
var mustAddAccount = addAccountEnv is not null && addAccountEnv.ToLower() == "true";

if (mustAddAccount || args.Length > 0 && args[0] == "--add-account")
{
    do
    {
        logger.Information("Do you want to add another account? (Y/N)");
        try
        {
            var answer = UserInput.ReadInput(["y", "n"]);

            if (answer == "n")
            {
                break;
            }
        }
        catch (Exception e)
        {
            logger.Error(e, e.Message);
            continue;
        }
        

        var response = await StartAuth();

        if (response == -1)
        {
            break;
        }
        
    } while (true);
}

async Task<int> StartAuth()
{
    logger.Information("Which platform");
    logger.Information("1. Twitch");
    logger.Information("2. Kick");
    logger.Information("3. Exit");
    try
    {
        int answer = Int32.Parse(UserInput.ReadInput(["1", "2", "3"]));
        
        switch (answer)
        {
            case 1:
                await Twitch.AuthTwitchDeviceAsync();
                break;
            case 2:
                SystemLoggerService.Logger.Information("Kick auth");
                await Kick.AuthKickDeviceAsync(logger);
                break;
            case 3:
                return -1;
        }
    }
    catch (Exception e)
    {
        Console.Write(e.Message);
    }

    return 1;
}

while (config.KickSettings.KickUsers.Count == 0 && config.TwitchSettings.TwitchUsers.Count == 0)
{
    logger.Information("No users found in the configuration file.");
    logger.Information("Login process will start.");

    var response = await StartAuth();
    
    if (response == -1)
    {
        break;
    }
}

var botTasks = new List<Task>();
var twitchUsers = config.TwitchSettings.TwitchUsers;
var twitchSettings = config.TwitchSettings;

if (twitchSettings is not null)
{
    foreach (var twitchUserSetting in twitchUsers)
    {
        if (!twitchUserSetting.Enabled)
        {
            logger.Information($"User {twitchUserSetting.Login} is not enabled, skipping...");
            continue;
        }

        var botUser = new TwitchUser(twitchUserSetting);
        var twitchBot = botUser.CreateBot();
        
        botTasks.Add(BotRunner.StartBot(twitchBot));
    }
}

var kickUsers = config.KickSettings.KickUsers;

foreach (var kickUserSettings in kickUsers)
{
    if (!kickUserSettings.Enabled)
    {
        logger.Information($"User {kickUserSettings.Login} is not enabled, skipping...");
        continue;
    }

    if (string.IsNullOrEmpty(kickUserSettings.Login))
    {
        var(id, username) = await KickHttpRepository.GetUserInfo(kickUserSettings.BearerToken);
        kickUserSettings.Id = id;
        kickUserSettings.Login = username;
        
        AppSettingsService.SaveConfig();
    }
        
    var kickUser = new KickUser(kickUserSettings);
    var kickBot = kickUser.CreateBot();
    
    botTasks.Add(BotRunner.StartBot(kickBot));
}

await Task.WhenAll(botTasks);