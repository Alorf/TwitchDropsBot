using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using TwitchDropsBot.Console.Platform;
using TwitchDropsBot.Console.Utils;
using TwitchDropsBot.Core.Platform.Kick.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Factories.User;
using TwitchDropsBot.Core.Platform.Shared.Helpers;
using TwitchDropsBot.Core.Platform.Shared.Services.Extensions;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using WatchRequest = TwitchDropsBot.Core.Platform.Kick.WatchManager.WatchRequest;

var builder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true);

var configuration = builder.Build();

var configFilePath = ConfigPathHelper.GetConfigFilePath("config.json"); // bot dynamic config

var botBuilder = new ConfigurationBuilder()
    .AddJsonFile(configFilePath, optional: false, reloadOnChange: true);

var botConfiguration = botBuilder.Build();

var services = new ServiceCollection();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

services.AddLogging(loggingBuilder =>
    loggingBuilder.ClearProviders()
        .AddSerilog(Log.Logger, dispose: true));

services.AddSingleton<IConfiguration>(configuration);
services.AddBotService();
services.AddTwitchService();
services.AddKickService();

var settingsManager = new SettingsManager(configFilePath);
services.AddSingleton(settingsManager);

await using var provider = services.BuildServiceProvider();

var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Main");

var settings = settingsManager.Read();

var addAccountEnv = Environment.GetEnvironmentVariable("ADD_ACCOUNT");
var mustAddAccount = addAccountEnv is not null && addAccountEnv.ToLower() == "true";

if (mustAddAccount || args.Length > 0 && args[0] == "--add-account")
{
    do
    {
        logger.LogInformation("Do you want to add another account? (Y/N)");
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
            logger.LogError(e, e.Message);
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
    logger.LogInformation("Which platform");
    logger.LogInformation("1. Twitch");
    logger.LogInformation("2. Kick");
    logger.LogInformation("3. Exit");
    try
    {
        int answer = Int32.Parse(UserInput.ReadInput(["1", "2", "3"]));

        switch (answer)
        {
            case 1:
                await Twitch.AuthTwitchDeviceAsync(settingsManager, logger);
                break;
            case 2:
                // SystemLoggerService.logger.LogInformation("Kick auth");
                await Kick.AuthKickDeviceAsync(logger, settingsManager);
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

while (settings.KickSettings.KickUsers.Count == 0 && settings.TwitchSettings.TwitchUsers.Count == 0)
{
    logger.LogInformation("No users found in the configuration file.");
    logger.LogInformation("Login process will start.");

    var response = await StartAuth();

    if (response == -1)
    {
        break;
    }
}

var userFactory = provider.GetRequiredService<UserFactory>();
var botTasks = new List<Task>();
var twitchUsers = settings.TwitchSettings.TwitchUsers;
var kickUsers = settings.KickSettings.KickUsers;

foreach (var twitchUserSetting in twitchUsers.Where(u => u.Enabled))
{
    var user = userFactory.CreateTwitchUser(twitchUserSetting);
    botTasks.Add(user.StartBot());
}

foreach (var kickUserSettings in kickUsers.Where(u => u.Enabled))
{
    var user = userFactory.CreateKickUser(kickUserSettings);
    botTasks.Add(user.StartBot());
}

await Task.WhenAll(botTasks);
