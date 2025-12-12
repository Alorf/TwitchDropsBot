using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Platform.Shared.Helpers;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Services.Extensions;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.WinForms
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
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
                .WriteTo.File($"logs/system-log.log")
                .CreateLogger();

            services.AddLogging(loggingBuilder =>
                loggingBuilder.ClearProviders()
                    .AddSerilog(Log.Logger, dispose: true));

            services.AddSingleton<IConfiguration>(configuration);
            services.Configure<BotSettings>(botConfiguration);
            services.AddSingleton<IOptionsChangeTokenSource<BotSettings>>(
                new ConfigurationChangeTokenSource<BotSettings>(Options.DefaultName, botConfiguration));
            
            services.AddBotService();
            services.AddTwitchService();
            services.AddKickService();
            
            services.AddTransient<MainForm>();


            var settingsManager = new SettingsManager(configFilePath);
            services.AddSingleton(settingsManager);

            var provider = services.BuildServiceProvider();
            
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

#if RELEASE
            try
            {

                Application.Run(provider.GetRequiredService<MainForm>());
            }
            catch (Exception e)
            {
                var logger = provider.GetRequiredService<ILogger<MainForm>>();
                logger.LogError(e, e.Message);
                Environment.Exit(1);
            }
#else

            Application.Run(provider.GetRequiredService<MainForm>());

#endif

        }
    }
}