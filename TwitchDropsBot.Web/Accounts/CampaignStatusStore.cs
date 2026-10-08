using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TwitchDropsBot.Web.Accounts;

/// <summary>
/// Campaigns each account has finished, kept in status.json in the web UI's data directory. Twitch only
/// lists recent claims, so without this a restart would forget campaigns finished days ago.
/// </summary>
public sealed class CampaignStatusStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _lock = new();
    private readonly string _path;
    private readonly ILogger _logger;
    private readonly Dictionary<string, Dictionary<string, DoneEntry>> _done;

    public CampaignStatusStore(string dataDirectory, ILogger logger)
    {
        _path = Path.Combine(dataDirectory, "status.json");
        _logger = logger;
        _done = Load();
    }

    public bool IsDone(string accountId, string campaignId)
    {
        lock (_lock)
        {
            return _done.TryGetValue(accountId, out var campaigns) && campaigns.ContainsKey(campaignId);
        }
    }

    public void MarkDone(string accountId, string campaignId, DateTime? endAt)
    {
        lock (_lock)
        {
            if (!_done.TryGetValue(accountId, out var campaigns))
            {
                campaigns = new Dictionary<string, DoneEntry>();
                _done[accountId] = campaigns;
            }

            if (campaigns.ContainsKey(campaignId))
            {
                return;
            }

            campaigns[campaignId] = new DoneEntry(endAt, DateTime.UtcNow);
            Save();
        }
    }

    /// <summary>Forgets campaigns that ended more than a day ago.</summary>
    public void Prune()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddDays(-1);
            var removed = 0;
            foreach (var campaigns in _done.Values)
            {
                foreach (var id in campaigns.Where(x => x.Value.EndAt is { } end && end < cutoff).Select(x => x.Key).ToList())
                {
                    campaigns.Remove(id);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Save();
            }
        }
    }

    private Dictionary<string, Dictionary<string, DoneEntry>> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<StatusFile>(File.ReadAllText(_path), JsonOptions)?.Done ?? new();
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Can't read {StatusFile}; finished campaigns will be found again", _path);
        }

        return new Dictionary<string, Dictionary<string, DoneEntry>>();
    }

    // Caller holds the lock
    private void Save()
    {
        try
        {
            var tempPath = _path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(new StatusFile { Done = _done }, JsonOptions));
            File.Move(tempPath, _path, overwrite: true);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Can't save {StatusFile}", _path);
        }
    }

    private sealed record DoneEntry(DateTime? EndAt, DateTime FoundAt);

    private sealed class StatusFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, Dictionary<string, DoneEntry>> Done { get; set; } = new();
    }
}
