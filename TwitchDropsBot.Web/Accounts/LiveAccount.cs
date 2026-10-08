using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;
using TwitchDropsBot.Core.Platform.Twitch.Models.Extensions;

namespace TwitchDropsBot.Web.Accounts;

/// <summary>A running bot account. Reads its state; never touches its credentials.</summary>
public sealed class LiveAccount : IAccount
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DetailsLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan TwitchTimeout = TimeSpan.FromSeconds(25);

    private readonly TwitchUser _user;
    private readonly IOptionsMonitor<BotSettings> _settings;
    private readonly CampaignStatusStore _store;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _statusLock = new(1, 1);
    private readonly ConcurrentDictionary<string, (DateTime At, DropCampaign Campaign)> _details = new();
    private readonly ConcurrentDictionary<string, string> _boxArt = new();
    private readonly ConcurrentDictionary<string, CampaignStatus> _checked = new();
    private readonly ConcurrentDictionary<string, StatusAttempt> _attempts = new();
    private long _statusVersion;

    private List<AbstractCampaign>? _refreshedCampaigns;
    private Inventory? _refreshedInventory;
    private DateTime? _refreshedAt;

    public LiveAccount(TwitchUser user, IOptionsMonitor<BotSettings> settings, CampaignStatusStore store)
    {
        _user = user;
        _settings = settings;
        _store = store;

        // The bot's own file log, so the Activity view starts with history rather than empty
        Logs.Preload(Path.Combine(Environment.CurrentDirectory, "logs", $"logs-TwitchUser-{user.Login}.log"));

        if (user.UISink is not null)
        {
            user.UISink.OnLogEvent += Logs.Add;
        }

        // Raised on the bot's thread as it publishes a new list, before it re-fetches each campaign it checks
        // and replaces the campaign's game with one that may lack box art
        user.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TwitchUser.AvailableCampaigns))
            {
                RememberBoxArt(user.AvailableCampaigns);
            }
            else if (e.PropertyName == nameof(TwitchUser.Inventory))
            {
                Interlocked.Increment(ref _statusVersion);
            }
        };
        RememberBoxArt(user.AvailableCampaigns);
    }

    public string? KnownBoxArt(string? gameId) =>
        gameId is not null && _boxArt.TryGetValue(gameId, out var url) ? url : null;

    private void RememberBoxArt(IEnumerable<AbstractCampaign> campaigns)
    {
        foreach (var campaign in campaigns)
        {
            if (campaign.Game is { Id: { } id, BoxArtUrl: { Length: > 0 } url })
            {
                _boxArt[id] = url;
            }
        }
    }

    public LogBuffer Logs { get; } = new();

    public string Id => _user.Id;
    public string Login => _user.Login;

    public BotStatus Status => _user.Status;
    public BotIdleReason IdleReason => _user.IdleReason;
    public DateTime? NextCycleAt => _user.NextCycleAt;
    public string? LastError => _user.LastError;
    public DateTime? LastErrorAt => _user.LastErrorAt;

    public AbstractCampaign? CurrentCampaign => _user.CurrentCampaign;
    public TimeBasedDrop? CurrentTimeBasedDrop => _user.CurrentTimeBasedDrop;
    public User? CurrentBroadcaster => _user.CurrentBroadcaster;
    public int? CurrentMinutesWatched => _user.CurrentMinutesWatched;
    public int? RequiredMinutesWatched => _user.RequiredMinutesWatched;
    public IReadOnlyList<DropProgressInfo>? CurrentDropsProgress => _user.CurrentDropsProgress;
    public AbstractCampaign? CheckingCampaign => _user.CheckingCampaign;

    // The bot's own list from its last cycle, unless a refresh from the UI is newer
    private bool UseRefreshed => _refreshedAt is not null &&
                                 (_user.AvailableCampaignsAt is null || _refreshedAt > _user.AvailableCampaignsAt);

    public IReadOnlyList<AbstractCampaign> Campaigns =>
        UseRefreshed ? _refreshedCampaigns! : _user.AvailableCampaigns;

    public DateTime? CampaignsAt => UseRefreshed ? _refreshedAt : _user.AvailableCampaignsAt;

    public Inventory? Inventory => UseRefreshed && _refreshedInventory is not null ? _refreshedInventory : _user.Inventory;

    public IReadOnlyDictionary<string, CampaignNote> Notes => _user.CampaignNotes;

    public IReadOnlyList<string> FavouriteGames => _user.FavouriteGames;

    public bool OnlyFavouriteGames => _settings.CurrentValue.TwitchSettings.OnlyFavouriteGames;

    public IReadOnlyList<string> AvoidedCampaigns => _settings.CurrentValue.TwitchSettings.AvoidCampaign;

    public IReadOnlyDictionary<string, CampaignStatus> CheckedStatus => _checked;

    public IReadOnlyDictionary<string, StatusAttempt> StatusAttempts => _attempts;

    public long StatusVersion => Interlocked.Read(ref _statusVersion);

    public bool IsKnownDone(string campaignId) => _store.IsDone(Id, campaignId);

    public void MarkDone(string campaignId, DateTime? endAt) => _store.MarkDone(Id, campaignId, endAt);

    public async Task RefreshCampaignsAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (CampaignsAt is { } at && DateTime.UtcNow - at < RefreshInterval)
            {
                return;
            }

            var campaigns = await _user.TwitchRepository.FetchDropsAsync()
                .WaitAsync(TwitchTimeout, cancellationToken);
            var inventory = await _user.TwitchRepository.FetchInventoryDropsAsync()
                .WaitAsync(TwitchTimeout, cancellationToken);

            RememberBoxArt(campaigns);
            if (inventory is not null)
            {
                RememberBoxArt(inventory.DropCampaignsInProgress);
            }

            _refreshedCampaigns = campaigns;
            _refreshedInventory = inventory;
            _refreshedAt = DateTime.UtcNow;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<AbstractCampaign?> GetCampaignDetailsAsync(string campaignId, CancellationToken cancellationToken)
    {
        var campaign = Campaigns.FirstOrDefault(x => x.Id == campaignId);
        if (campaign is not DropCampaign || campaign.TimeBasedDrops.Count > 0)
        {
            return campaign;
        }

        if (_details.TryGetValue(campaignId, out var cached) && DateTime.UtcNow - cached.At < DetailsLifetime)
        {
            return cached.Campaign;
        }

        var details = await _user.TwitchRepository.FetchTimeBasedDropsAsync(campaignId)
            .WaitAsync(TwitchTimeout, cancellationToken);
        if (details is null)
        {
            return campaign;
        }

        _details[campaignId] = (DateTime.UtcNow, details);
        return details;
    }

    // Twitch's inventory forgets a campaign once every drop is claimed and lists only the latest claims,
    // so the progress a live channel reports for it is the one reliable source
    public async Task<CampaignStatus?> CheckStatusAsync(string campaignId, TimeSpan maxAge,
        CancellationToken cancellationToken)
    {
        bool Fresh(out CampaignStatus? status)
        {
            var found = _checked.TryGetValue(campaignId, out var known);
            status = known;
            if (found && DateTime.UtcNow - known!.CheckedAt < maxAge)
            {
                return true;
            }

            return _attempts.TryGetValue(campaignId, out var attempt) && attempt.Problem is not null &&
                   DateTime.UtcNow - attempt.At < maxAge;
        }

        if (Fresh(out var current) || Campaigns.FirstOrDefault(x => x.Id == campaignId) is not DropCampaign listed)
        {
            return current;
        }

        await _statusLock.WaitAsync(cancellationToken);
        try
        {
            if (Fresh(out current))
            {
                return current;
            }

            var campaign = await GetCampaignDetailsAsync(campaignId, cancellationToken) ?? listed;
            var channel = await FindLiveChannelAsync(campaign, cancellationToken);
            if (channel?.Id is null)
            {
                _attempts[campaignId] = new StatusAttempt(DateTime.UtcNow, "No live channel to check right now");
                return current;
            }

            var results = await _user.TwitchRepository.FetchDropCampaignsProgressAsync(channel)
                .WaitAsync(TwitchTimeout, cancellationToken);
            var channelName = channel.DisplayName ?? channel.Login;
            var now = DateTime.UtcNow;

            // One channel answers for every campaign it carries, usually all of the game's
            foreach (var result in results.Where(x => x.Id is not null))
            {
                var status = CampaignStatus.FromChannel(result, channelName);
                if (!_checked.TryGetValue(result.Id, out var previous) || !previous.Tiers.SequenceEqual(status.Tiers))
                {
                    Interlocked.Increment(ref _statusVersion);
                }

                _checked[result.Id] = status;
                _attempts[result.Id] = new StatusAttempt(now, null);
                if (status.AllClaimed)
                {
                    var listedResult = Campaigns.FirstOrDefault(x => x.Id == result.Id);
                    MarkDone(result.Id, listedResult?.EndAt ?? listedResult?.EndsAt ?? result.EndAt);
                }
            }

            if (!results.Any(x => x.Id == campaignId))
            {
                _attempts[campaignId] = new StatusAttempt(now, $"{channelName} doesn't report this campaign");
            }

            return _checked.GetValueOrDefault(campaignId);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _attempts[campaignId] = new StatusAttempt(DateTime.UtcNow, "Twitch didn't answer");
            return current;
        }
        finally
        {
            _statusLock.Release();
        }
    }

    // A channel the campaign counts on: one of its own when it is limited to some, else the busiest in its game
    private async Task<User?> FindLiveChannelAsync(AbstractCampaign campaign, CancellationToken cancellationToken)
    {
        var repository = _user.TwitchRepository;
        var channels = campaign.Allow?.Channels;
        if (channels is { Count: > 0 and < 250 })
        {
            foreach (var group in channels.Select(x => x.Name).OfType<string>().Chunk(10).Take(5))
            {
                var users = await repository.FetchStreamInformationAsync(group)
                    .WaitAsync(TwitchTimeout, cancellationToken);
                var live = users?.Where(x => x.IsLive()).ToList() ?? new List<User>();
                var match = live.FirstOrDefault(x => x.BroadcastSettings?.Game?.Id == campaign.Game?.Id) ??
                            live.FirstOrDefault();
                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }

        if (string.IsNullOrEmpty(campaign.Game?.Slug))
        {
            return null;
        }

        var game = await repository.FetchDirectoryPageGameAsync(campaign.Game.Slug, true)
            .WaitAsync(TwitchTimeout, cancellationToken);
        return game?.Streams?.Edges?
            .Select(x => x.Node)
            .Where(x => x?.Broadcaster is not null)
            .OrderByDescending(x => x!.ViewersCount)
            .FirstOrDefault()?.Broadcaster;
    }

    public void RequestReselect() => _user.RequestReselect();
}

public sealed class LiveAccountSource : IAccountSource
{
    private readonly BotRegistry _registry;
    private readonly IOptionsMonitor<BotSettings> _settings;
    private readonly CampaignStatusStore _store;
    private readonly ConcurrentDictionary<TwitchUser, LiveAccount> _accounts = new();

    public LiveAccountSource(BotRegistry registry, CampaignQueueService queue, IOptionsMonitor<BotSettings> settings,
        CampaignStatusStore store)
    {
        _registry = registry;
        _settings = settings;
        _store = store;
        Queue = queue;

        // Subscribe before reading the list, so no account's early log lines are missed
        _registry.UserRegistered += user =>
        {
            if (user is TwitchUser twitchUser)
            {
                Get(twitchUser);
            }
        };

        foreach (var user in _registry.Users.OfType<TwitchUser>())
        {
            Get(user);
        }
    }

    public CampaignQueueService Queue { get; }

    public CampaignStatusStore Store => _store;

    public IReadOnlyList<IAccount> Accounts => _registry.Users.OfType<TwitchUser>().Select(Get).ToList();

    private LiveAccount Get(TwitchUser user) => _accounts.GetOrAdd(user, x => new LiveAccount(x, _settings, _store));
}
