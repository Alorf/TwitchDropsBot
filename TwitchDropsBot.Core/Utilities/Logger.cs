using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using TwitchDropsBot.Core.Object;

namespace TwitchDropsBot.Core;

public class Logger
{
    private TwitchUser TwitchUser { get; set; }

    public event Action<string> OnLog;
    public event Action<string> OnError;
    public event Action<System.Exception> OnException;
    public event Action<string> OnInfo;

    private readonly ILogger _logger;

    public Logger(TwitchUser twitchUser)
    {
        TwitchUser = twitchUser;
        _logger = new LoggerConfiguration()
            .WriteTo.Console(theme: SystemConsoleTheme.Colored)
            .WriteTo.File($"logs/{TwitchUser.Login}.txt")
            .CreateLogger();
    }

    public void Log(string message)
    {
        _logger.Information($"[{TwitchUser.Login}] : {message}");
        OnLog?.Invoke(message);
    }

    public void Log(string message, string type, ConsoleColor color)
    {
        _logger.Information($"[{TwitchUser.Login}] {type} : {message}");
        OnLog?.Invoke(message);
    }

    public void Error(string message)
    {
        _logger.Error($"[{TwitchUser.Login}] : {message}");
        OnError?.Invoke(message);
    }

    public void Error(System.Exception exception)
    {
        _logger.Error(exception, $"[{TwitchUser.Login}] ");
        OnException?.Invoke(exception);
    }

    public void Info(string message)
    {
        _logger.Information($"[{TwitchUser.Login}] : {message}");
        OnInfo?.Invoke(message);
    }
}