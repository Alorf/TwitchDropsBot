using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Parsing;
using TwitchDropsBot.Web.Api;

namespace TwitchDropsBot.Web.Accounts;

/// <summary>The most recent log lines of one account, for the Activity view.</summary>
public sealed partial class LogBuffer
{
    private readonly object _lock = new();
    private readonly Queue<LogLineDto> _lines = new();
    private readonly int _capacity;
    private long _seq;

    public LogBuffer(int capacity = 500)
    {
        _capacity = capacity;
    }

    public void Add(LogEventLevel level, string message, DateTime? at = null)
    {
        lock (_lock)
        {
            _lines.Enqueue(new LogLineDto(++_seq, at ?? DateTime.UtcNow, LevelName(level), Redactor.Clean(message)));
            while (_lines.Count > _capacity)
            {
                _lines.Dequeue();
            }
        }
    }

    public void Add(LogEvent logEvent) => Add(logEvent.Level, Render(logEvent), logEvent.Timestamp.UtcDateTime);

    /// <summary>
    /// Starts the buffer with the end of the account's log file, so the Activity view isn't empty after a
    /// restart. Lines look like "2026-01-31 18:04:05.123 +00:00 [INF] message"; continuation lines are skipped.
    /// </summary>
    public void Preload(string path, int lines = 200)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            const int tailBytes = 256 * 1024;
            if (stream.Length > tailBytes)
            {
                stream.Seek(-tailBytes, SeekOrigin.End);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = reader.ReadToEnd();
            var entries = new List<(DateTime At, LogEventLevel Level, string Message)>();
            foreach (var line in text.Split('\n'))
            {
                var match = FileLine().Match(line.TrimEnd('\r'));
                if (!match.Success ||
                    !DateTimeOffset.TryParseExact(match.Groups["at"].Value, "yyyy-MM-dd HH:mm:ss.fff zzz",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
                {
                    continue;
                }

                entries.Add((at.UtcDateTime, match.Groups["level"].Value switch
                {
                    "VRB" or "DBG" => LogEventLevel.Debug,
                    "INF" => LogEventLevel.Information,
                    "WRN" => LogEventLevel.Warning,
                    _ => LogEventLevel.Error
                }, match.Groups["message"].Value));
            }

            foreach (var entry in entries.TakeLast(lines))
            {
                Add(entry.Level, entry.Message, entry.At);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // Like LogEvent.RenderMessage, but strings are written as they are instead of in quotes
    private static string Render(LogEvent logEvent)
    {
        var builder = new StringBuilder();
        using var writer = new StringWriter(builder);
        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            if (token is PropertyToken property &&
                logEvent.Properties.TryGetValue(property.PropertyName, out var value) &&
                value is ScalarValue { Value: string text })
            {
                writer.Write(text);
            }
            else
            {
                token.Render(logEvent.Properties, writer);
            }
        }

        return builder.ToString();
    }

    public LogPageDto After(long seq, int max = 300)
    {
        lock (_lock)
        {
            var lines = _lines.Where(x => x.Seq > seq).TakeLast(max).ToList();
            return new LogPageDto(lines, _seq);
        }
    }

    private static string LevelName(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose or LogEventLevel.Debug => "debug",
        LogEventLevel.Information => "info",
        LogEventLevel.Warning => "warning",
        _ => "error"
    };

    [GeneratedRegex(@"^(?<at>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(?<level>[A-Z]{3})\] (?<message>.*)$")]
    private static partial Regex FileLine();
}
