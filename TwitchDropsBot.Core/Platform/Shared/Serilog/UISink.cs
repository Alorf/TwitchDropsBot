using Serilog.Core;
using Serilog.Events;

namespace TwitchDropsBot.Core.Platform.Shared.Serilog;

public class UISink : ILogEventSink
{
    public event Action<string, LogEventLevel> OnLogReceived;

    // The whole event, for front-ends that render it themselves
    public event Action<LogEvent>? OnLogEvent;

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        OnLogReceived?.Invoke(message, logEvent.Level);
        OnLogEvent?.Invoke(logEvent);
    }
}