using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using TwitchDropsBot.Console;
using TwitchDropsBot.Console.Platform;
using TwitchDropsBot.Console.Utils;
using TwitchDropsBot.Core.Platform.Shared.Factories.User;
using TwitchDropsBot.Core.Platform.Shared.Helpers;
using TwitchDropsBot.Core.Platform.Shared.Services.Extensions;
using TwitchDropsBot.Core.Platform.Shared.Settings;

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

services.AddLogging(
    loggingBuilder => 
        loggingBuilder.ClearProviders()
            .AddSerilog(Log.Logger, dispose: true)
    );

services.AddSingleton<IConfiguration>(configuration);
services.Configure<BotSettings>(botConfiguration);
services.AddSingleton<IOptionsChangeTokenSource<BotSettings>>(
    new ConfigurationChangeTokenSource<BotSettings>(Options.DefaultName, botConfiguration));

services.AddBotService();
services.AddTwitchService();
services.AddKickService();

var settingsManager = new SettingsManager(configFilePath);
services.AddSingleton(settingsManager);

await using var provider = services.BuildServiceProvider();

var start = new Start(
    provider.GetRequiredService<IOptionsMonitor<BotSettings>>(),
    provider.GetRequiredService<ILogger<Start>>(),
    provider.GetRequiredService<SettingsManager>(),
    provider.GetRequiredService<UserFactory>(),
    args
);

await start.StartAsync();