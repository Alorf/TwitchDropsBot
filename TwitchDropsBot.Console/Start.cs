using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Console.Platform;
using TwitchDropsBot.Console.Utils;
using TwitchDropsBot.Core.Platform.Shared.Factories.User;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Console;

public class Start
{
    private readonly IOptionsMonitor<BotSettings> _botSettings;
    private readonly ILogger<Start> logger;
    private readonly SettingsManager settingsManager;
    private readonly UserFactory _userFactory;
    private readonly string[] args;

    public Start(IOptionsMonitor<BotSettings> botSettings,
        ILogger<Start> logger,
        SettingsManager settingsManager,
        UserFactory userFactory,
        string[] args)
    {
        _botSettings = botSettings;
        this.logger = logger;
        this.settingsManager = settingsManager;
        _userFactory = userFactory;
        this.args = args;
    }

    public async Task StartAsync()
    {
        await HandleAccountAdditionAsync();
        await EnsureUsersExistAsync();
        await StartBotsAsync();
    }

    private async Task HandleAccountAdditionAsync()
    {
        if (!ShouldAddAccount())
            return;

        do
        {
            logger.LogInformation("Do you want to add another account? (Y/N)");
            
            try
            {
                var answer = UserInput.ReadInput(["y", "n"]);
                if (answer == "n")
                    break;
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                continue;
            }

            var response = await StartAuthAsync();
            if (response == -1)
                break;

        } while (true);
    }

    private async Task EnsureUsersExistAsync()
    {
        while (HasNoUsers(_botSettings.CurrentValue))
        {
            logger.LogInformation("No users found in the configuration file.");
            logger.LogInformation("Login process will start.");

            var response = await StartAuthAsync();
            if (response == -1)
                break;
        }
    }

    private async Task StartBotsAsync()
    {
        var botTasks = new List<Task>();

        var twitchUsers = _botSettings.CurrentValue.TwitchSettings.TwitchUsers;
        var kickUsers = _botSettings.CurrentValue.KickSettings.KickUsers;

        foreach (var twitchUserSetting in twitchUsers.Where(u => u.Enabled))
        {
            var user = _userFactory.CreateTwitchUser(twitchUserSetting);
            botTasks.Add(user.StartBot());
        }

        foreach (var kickUserSettings in kickUsers.Where(u => u.Enabled))
        {
            var user = _userFactory.CreateKickUser(kickUserSettings);
            botTasks.Add(user.StartBot());
        }

        await Task.WhenAll(botTasks);
    }

    private bool ShouldAddAccount()
    {
        var addAccountEnv = Environment.GetEnvironmentVariable("ADD_ACCOUNT");
        var mustAddAccount = addAccountEnv is not null && addAccountEnv.ToLower() == "true";
        
        return mustAddAccount || (args.Length > 0 && args[0] == "--add-account");
    }

    private static bool HasNoUsers(BotSettings settings)
    {
        return settings.KickSettings.KickUsers.Count == 0 && 
               settings.TwitchSettings.TwitchUsers.Count == 0;
    }

    private async Task<int> StartAuthAsync()
    {
        logger.LogInformation("Which platform");
        logger.LogInformation("1. Twitch");
        logger.LogInformation("2. Kick");
        logger.LogInformation("3. Exit");

        try
        {
            int answer = int.Parse(UserInput.ReadInput(["1", "2", "3"]));

            return answer switch
            {
                1 => await AuthenticateTwitchAsync(),
                2 => await AuthenticateKickAsync(),
                3 => -1,
                _ => 1
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return 1;
        }
    }

    private async Task<int> AuthenticateTwitchAsync()
    {
        await Twitch.AuthTwitchDeviceAsync(settingsManager, logger);
        await Task.Delay(1000);

        return 1;
    }

    private async Task<int> AuthenticateKickAsync()
    {
        await Kick.AuthKickDeviceAsync(logger, settingsManager);
        await Task.Delay(1000);

        return 1;
    }
}
