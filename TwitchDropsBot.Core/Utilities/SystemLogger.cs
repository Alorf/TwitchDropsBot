using Serilog;
using TwitchDropsBot.Core.Object;

namespace TwitchDropsBot.Core;

public class SystemLogger
{
    private static readonly ILogger _logger;

    static SystemLogger()
    {
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/system-log.txt")
            .CreateLogger();
    }

    public static void Log(string message)
    {
        _logger.Information(message);
    }

    public static void Error(string message)
    {
        _logger.Error(message);
    }

    public static void Error(System.Exception exception)
    {
        _logger.Error(exception, exception.Message);
    }

    public static void Info(string message)
    {
        _logger.Information(message);
    }
}