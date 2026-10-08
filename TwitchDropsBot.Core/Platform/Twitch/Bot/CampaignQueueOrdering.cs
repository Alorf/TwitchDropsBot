using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;

namespace TwitchDropsBot.Core.Platform.Twitch.Bot;

public enum CampaignNoteKind
{
    NoLiveChannel,
    Completed,
    NotEnoughTime,
    NoWatchableDrops,
    NothingLeftOnChannel,
    WatchedNotClaimed,
    CategoryNotFound
}

/// <summary>Why the bot last passed over a campaign, shown in the web UI.</summary>
public record CampaignNote(CampaignNoteKind Kind, DateTime At);

public static class CampaignQueueOrdering
{
    /// <summary>The queued campaigns found among this cycle's campaigns, in queue order.</summary>
    public static List<AbstractCampaign> FindQueued(IEnumerable<AbstractCampaign> campaigns,
        IReadOnlyList<string> queuedCampaignIds)
    {
        var byId = campaigns.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());

        return queuedCampaignIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToList();
    }

    /// <summary>
    /// Queued campaigns first, in queue order, then the normal order without them.
    /// A queued campaign removed by the favourites or linked-account filters is put back.
    /// </summary>
    public static List<AbstractCampaign> PutFirst(List<AbstractCampaign> ordered, List<AbstractCampaign> queued)
    {
        if (queued.Count == 0)
        {
            return ordered;
        }

        var queuedIds = queued.Select(x => x.Id).ToHashSet();
        return queued.Concat(ordered.Where(x => !queuedIds.Contains(x.Id))).ToList();
    }
}
