using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;

namespace TwitchDropsBot.Web.Accounts;

/// <summary>
/// One farming account as the web UI sees it: a running bot, or a simulated one in demo mode.
/// </summary>
public interface IAccount
{
    string Id { get; }
    string Login { get; }

    BotStatus Status { get; }
    BotIdleReason IdleReason { get; }
    DateTime? NextCycleAt { get; }
    string? LastError { get; }
    DateTime? LastErrorAt { get; }

    AbstractCampaign? CurrentCampaign { get; }
    TimeBasedDrop? CurrentTimeBasedDrop { get; }
    User? CurrentBroadcaster { get; }
    int? CurrentMinutesWatched { get; }
    int? RequiredMinutesWatched { get; }
    IReadOnlyList<DropProgressInfo>? CurrentDropsProgress { get; }
    AbstractCampaign? CheckingCampaign { get; }

    IReadOnlyList<AbstractCampaign> Campaigns { get; }
    DateTime? CampaignsAt { get; }
    Inventory? Inventory { get; }
    IReadOnlyDictionary<string, CampaignNote> Notes { get; }
    IReadOnlyList<string> FavouriteGames { get; }
    bool OnlyFavouriteGames { get; }
    IReadOnlyList<string> AvoidedCampaigns { get; }

    LogBuffer Logs { get; }

    /// <summary>Box art last seen for a game, for campaigns whose game object no longer carries it.</summary>
    string? KnownBoxArt(string? gameId);

    /// <summary>Progress per campaign found by asking a live channel, kept until the next check.</summary>
    IReadOnlyDictionary<string, CampaignStatus> CheckedStatus { get; }

    /// <summary>When a status check last ran for a campaign, and why it found nothing if it didn't.</summary>
    IReadOnlyDictionary<string, StatusAttempt> StatusAttempts { get; }

    /// <summary>
    /// Goes up whenever a campaign's progress may read differently: a status check found something new or the
    /// bot fetched a new inventory. The page reloads the campaign list when it changes.
    /// </summary>
    long StatusVersion { get; }

    /// <summary>Whether this account finished the campaign, as recorded by an earlier check.</summary>
    bool IsKnownDone(string campaignId);

    /// <summary>Records a campaign every drop of which is claimed.</summary>
    void MarkDone(string campaignId, DateTime? endAt);

    /// <summary>Asks a live channel for the campaign's progress, unless a check is newer than maxAge.</summary>
    Task<CampaignStatus?> CheckStatusAsync(string campaignId, TimeSpan maxAge, CancellationToken cancellationToken);

    /// <summary>Fetches the campaign list and inventory now instead of waiting for the next cycle.</summary>
    Task RefreshCampaignsAsync(CancellationToken cancellationToken);

    /// <summary>The campaign with its drops, fetched if this cycle's list doesn't carry them yet.</summary>
    Task<AbstractCampaign?> GetCampaignDetailsAsync(string campaignId, CancellationToken cancellationToken);

    void RequestReselect();
}

public sealed record StatusAttempt(DateTime At, string? Problem);

public interface IAccountSource
{
    IReadOnlyList<IAccount> Accounts { get; }
    CampaignQueueService Queue { get; }
    CampaignStatusStore Store { get; }
}
