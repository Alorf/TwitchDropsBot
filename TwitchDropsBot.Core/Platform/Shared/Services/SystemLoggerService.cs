using Serilog;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

public static class SystemLoggerService
{
    public static readonly ILogger Logger;

    static SystemLoggerService()
    {
        var appConfig = AppService.GetConfiguration();

        var loggerConfig = new LoggerConfiguration()
            .WriteTo.File($"logs/system-log.txt")
            .ReadFrom.Configuration(appConfig);

        Logger = loggerConfig.CreateLogger();
    }
}