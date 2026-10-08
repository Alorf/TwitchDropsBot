using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TwitchDropsBot.Web.Api;

/// <summary>
/// Slowly asks live channels where each queued and favourite campaign stands, one campaign at a time,
/// so finished campaigns show as finished even after Twitch has dropped them from the inventory.
/// </summary>
public sealed class StatusScanner : BackgroundService
{
    private readonly WebBackend _backend;
    private readonly ILogger<StatusScanner> _logger;
    private readonly TimeSpan _interval;

    public StatusScanner(WebBackend backend, ILogger<StatusScanner> logger)
    {
        _backend = backend;
        _logger = logger;
        _interval = backend.IsDemo ? TimeSpan.FromSeconds(4) : TimeSpan.FromSeconds(25);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the bots start and fetch their first campaign lists
            await Task.Delay(_backend.IsDemo ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(60), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var next = _backend.NextStatusCheck();
                if (next is { } check)
                {
                    try
                    {
                        await check.Account.CheckStatusAsync(check.CampaignId, WebBackend.RecheckAfter, stoppingToken);
                        _backend.NotifyChanged();
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        _logger.LogDebug(e, "Status check failed");
                    }
                }

                await Task.Delay(next is null ? TimeSpan.FromMinutes(2) : _interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
