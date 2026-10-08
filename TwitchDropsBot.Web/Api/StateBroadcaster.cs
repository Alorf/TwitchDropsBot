using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TwitchDropsBot.Web.Api;

/// <summary>
/// Rebuilds the state every second (or at once after a change) and pushes it to connected pages when it differs.
/// </summary>
public sealed class StateBroadcaster : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(1);

    private readonly WebBackend _backend;
    private readonly JsonSerializerOptions _json;
    private readonly ILogger<StateBroadcaster> _logger;
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _buildLock = new();
    private string? _last;
    private DateTime _lastMaintenance = DateTime.MinValue;

    public StateBroadcaster(WebBackend backend, JsonSerializerOptions json, ILogger<StateBroadcaster> logger)
    {
        _backend = backend;
        _json = json;
        _logger = logger;
        _backend.Changed += () => _signal.Release();
    }

    public string Current
    {
        get
        {
            lock (_buildLock)
            {
                return _last ??= Build();
            }
        }
    }

    public (Guid Id, ChannelReader<string> Reader) Subscribe()
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        var id = Guid.NewGuid();
        _subscribers[id] = channel;
        channel.Writer.TryWrite(Current);
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                if (DateTime.UtcNow - _lastMaintenance > MaintenanceInterval)
                {
                    _lastMaintenance = DateTime.UtcNow;
                    _backend.Maintain();
                }

                if (_subscribers.IsEmpty)
                {
                    lock (_buildLock)
                    {
                        _last = null;
                    }

                    continue;
                }

                string state;
                lock (_buildLock)
                {
                    state = Build();
                    if (state == _last)
                    {
                        continue;
                    }

                    _last = state;
                }

                foreach (var channel in _subscribers.Values)
                {
                    channel.Writer.TryWrite(state);
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Building the web UI state failed");
            }
        }
    }

    private string Build() => JsonSerializer.Serialize(_backend.GetState(), _json);
}
