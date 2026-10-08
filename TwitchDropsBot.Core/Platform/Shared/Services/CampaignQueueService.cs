using System.Text.Json;
using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Shared.Helpers;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

public record QueuedCampaign(
    string CampaignId,
    string? CampaignName,
    string? GameName,
    string? BoxArtUrl,
    DateTime? EndAt,
    DateTime AddedAt);

/// <summary>
/// Per-account list of campaigns to watch before the normal order, edited from the web UI.
/// Kept in queue.json next to config.json, which is never written here.
/// </summary>
public class CampaignQueueService
{
    private const string FileName = "queue.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _lock = new();
    private readonly ILogger<CampaignQueueService> _logger;
    private readonly string _filePath;
    private readonly Dictionary<string, List<QueuedCampaign>> _queues;

    public event Action<string>? Changed;

    public CampaignQueueService(ILogger<CampaignQueueService> logger, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? Path.Combine(DataDirectory(), FileName);
        _queues = Load();
    }

    /// <summary>
    /// Where the web UI keeps its files: WEBUI_DATA_DIR if set (so config.json can be mounted read-only),
    /// otherwise the directory of config.json.
    /// </summary>
    public static string DataDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("WEBUI_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Directory.CreateDirectory(configured);
            return configured;
        }

        return Path.GetDirectoryName(ConfigPathHelper.GetConfigFilePath("config.json"))!;
    }

    public IReadOnlyList<QueuedCampaign> Get(string accountId)
    {
        lock (_lock)
        {
            return _queues.TryGetValue(accountId, out var queue) ? queue.ToList() : new List<QueuedCampaign>();
        }
    }

    public IReadOnlyList<string> GetCampaignIds(string accountId)
    {
        return Get(accountId).Select(x => x.CampaignId).ToList();
    }

    /// <summary>Adds the campaign, or moves it if already queued. A null position means the end.</summary>
    public IReadOnlyList<QueuedCampaign> Add(string accountId, QueuedCampaign campaign, int? position = null)
    {
        return Update(accountId, queue =>
        {
            var existing = queue.FindIndex(x => x.CampaignId == campaign.CampaignId);
            if (existing >= 0)
            {
                campaign = campaign with { AddedAt = queue[existing].AddedAt };
                queue.RemoveAt(existing);
            }

            var index = Math.Clamp(position ?? queue.Count, 0, queue.Count);
            queue.Insert(index, campaign);
            return true;
        });
    }

    public IReadOnlyList<QueuedCampaign> Remove(string accountId, string campaignId)
    {
        return Update(accountId, queue => queue.RemoveAll(x => x.CampaignId == campaignId) > 0);
    }

    public IReadOnlyList<QueuedCampaign> RemoveWhere(string accountId, Func<QueuedCampaign, bool> predicate)
    {
        return Update(accountId, queue => queue.RemoveAll(x => predicate(x)) > 0);
    }

    /// <summary>Puts the queue in the given order; the ids must be exactly the queued campaigns.</summary>
    public IReadOnlyList<QueuedCampaign> Reorder(string accountId, IReadOnlyList<string> campaignIds)
    {
        return Update(accountId, queue =>
        {
            if (campaignIds.Count != queue.Count || campaignIds.Distinct().Count() != campaignIds.Count ||
                !campaignIds.All(id => queue.Any(x => x.CampaignId == id)))
            {
                throw new ArgumentException("The new order must list every queued campaign exactly once.");
            }

            var reordered = campaignIds.Select(id => queue.First(x => x.CampaignId == id)).ToList();
            if (reordered.SequenceEqual(queue))
            {
                return false;
            }

            queue.Clear();
            queue.AddRange(reordered);
            return true;
        });
    }

    private IReadOnlyList<QueuedCampaign> Update(string accountId, Func<List<QueuedCampaign>, bool> change)
    {
        List<QueuedCampaign> result;
        bool changed;

        lock (_lock)
        {
            if (!_queues.TryGetValue(accountId, out var queue))
            {
                queue = new List<QueuedCampaign>();
            }

            var working = queue.ToList();
            changed = change(working);

            if (changed)
            {
                if (working.Count > 0)
                {
                    _queues[accountId] = working;
                }
                else
                {
                    _queues.Remove(accountId);
                }

                Save();
            }

            result = working;
        }

        if (changed)
        {
            Changed?.Invoke(accountId);
        }

        return result;
    }

    private Dictionary<string, List<QueuedCampaign>> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, List<QueuedCampaign>>();
            }

            var file = JsonSerializer.Deserialize<QueueFile>(File.ReadAllText(_filePath), JsonOptions);
            return file?.Accounts ?? new Dictionary<string, List<QueuedCampaign>>();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Can't read {QueueFile}, starting with an empty queue", _filePath);
            return new Dictionary<string, List<QueuedCampaign>>();
        }
    }

    // Caller holds the lock
    private void Save()
    {
        try
        {
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(new QueueFile { Accounts = _queues }, JsonOptions));
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Can't save {QueueFile}; the queue is kept in memory until the next change", _filePath);
        }
    }

    private class QueueFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, List<QueuedCampaign>> Accounts { get; set; } = new();
    }
}
