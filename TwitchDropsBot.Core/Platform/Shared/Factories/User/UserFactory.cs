using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Settings;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Settings;

namespace TwitchDropsBot.Core.Platform.Shared.Factories.User;

public class UserFactory
{
    private readonly IServiceProvider _serviceProvider;


    public UserFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private Microsoft.Extensions.Logging.ILogger CreateLogger(string platform, BaseUserSettings settings, UISink? uiSink = null)
    {
        var serilogLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(_serviceProvider.GetRequiredService<IConfiguration>())
            .WriteTo.File($"logs/logs-{platform}-{settings.Login}.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .Enrich.WithProperty("User", settings.Login)
            .Enrich.WithProperty("UserType", platform);

        if (uiSink is not null)
        {
            serilogLogger.WriteTo.Sink(uiSink);
        }
        
        var buildLogger = serilogLogger.CreateLogger();
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(buildLogger, dispose: true);
        });
        
        return loggerFactory.CreateLogger($"{platform}-{settings.Login}");
    }

    public TwitchUser CreateTwitchUser(TwitchUserSettings settings, bool addSink = false)
    {
        if (addSink)
        {
            UISink sink = new UISink();    
            var loggersink = CreateLogger(typeof(TwitchUser).Name, settings, sink);
            return ActivatorUtilities.CreateInstance<TwitchUser>(_serviceProvider, settings, loggersink, sink);

        }

        var logger = CreateLogger(typeof(TwitchUser).Name, settings);
        return ActivatorUtilities.CreateInstance<TwitchUser>(_serviceProvider, settings, logger);
    }

    public KickUser CreateKickUser(KickUserSettings settings)
    {
        var logger = CreateLogger(typeof(KickUser).Name, settings);
        return ActivatorUtilities.CreateInstance<KickUser>(_serviceProvider, settings, logger);
    }
}