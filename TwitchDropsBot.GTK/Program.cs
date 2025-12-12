using Gtk;
using System;
using System.Reflection;
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

namespace TwitchDropsBot.GTK
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
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
            services.AddTransient<MainWindow>();


            var settingsManager = new SettingsManager(configFilePath);
            services.AddSingleton(settingsManager);

            var provider = services.BuildServiceProvider();
            
            // Initialize GTK
            Application.Init();

            var app = new Application("org.TwitchDropsBot.GTK.TwitchDropsBot.GTK", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            // var win = new MainWindow();
            var win = provider.GetRequiredService<MainWindow>();
            app.AddWindow(win);

            // Enable dark theme
            var settings = Settings.Default;
            settings.SetProperty("gtk-application-prefer-dark-theme", new GLib.Value(true));

            // Load the embedded resource
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream("TwitchDropsBot.GTK.images.logo.png"))
            {
                if (stream != null)
                {
                    win.Icon = new Gdk.Pixbuf(stream);
                }
                else
                {
                    Console.WriteLine("Resource not found.");
                }
            }
            win.Show();

#if RELEASE
            try
            {
                Application.Run();
            }
            catch (Exception e)
            {
                var logger = provider.GetRequiredService<ILogger<Program>>();
                logger.LogError(e, e.Message);
                Environment.Exit(1);
            }
#else
            Application.Run();
#endif
            
        }
    }
}
