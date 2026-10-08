using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;

namespace TwitchDropsBot.Web.Accounts;

/// <summary>
/// WEBUI_DEMO=true: two simulated accounts with made-up campaigns, for working on the page without
/// Twitch credentials. Nothing here talks to Twitch; box art links point at Twitch's public CDN.
/// </summary>
public sealed class DemoAccountSource : IAccountSource, IDisposable
{
    private readonly List<DemoAccount> _accounts;
    private readonly Timer _timer;

    public DemoAccountSource(ILoggerFactory loggerFactory)
    {
        var queuePath = Path.Combine(Path.GetTempPath(), $"tdb-demo-queue-{Environment.ProcessId}.json");
        Queue = new CampaignQueueService(loggerFactory.CreateLogger<CampaignQueueService>(), queuePath);
        var statusDirectory = Directory.CreateTempSubdirectory("tdb-demo-status").FullName;
        Store = new CampaignStatusStore(statusDirectory, loggerFactory.CreateLogger<CampaignStatusStore>());

        var catalogue = DemoCatalogue.Build();
        _accounts = new List<DemoAccount>
        {
            new("100000001", "ana_demo", catalogue, Queue, Store, DemoCatalogue.FirstAccountSetup),
            new("100000002", "ben_demo", catalogue, Queue, Store, DemoCatalogue.SecondAccountSetup)
        };

        _timer = new Timer(_ => Step(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    public CampaignQueueService Queue { get; }

    public CampaignStatusStore Store { get; }

    public IReadOnlyList<IAccount> Accounts => _accounts;

    private void Step()
    {
        foreach (var account in _accounts)
        {
            account.Step();
        }
    }

    public void Dispose() => _timer.Dispose();
}

internal sealed class DemoAccount : IAccount
{
    private readonly object _lock = new();
    private readonly CampaignQueueService _queue;
    private readonly List<AbstractCampaign> _campaigns;
    private readonly Dictionary<string, CampaignNote> _notes = new();
    private readonly Dictionary<string, int[]> _progress = new();   // campaign id -> minutes per tier
    private readonly HashSet<string> _offline;
    private readonly HashSet<string> _linked;
    private readonly HashSet<string> _unclaimed;
    private readonly CampaignStatusStore _store;
    private readonly ConcurrentDictionary<string, CampaignStatus> _checked = new();
    private readonly ConcurrentDictionary<string, StatusAttempt> _attempts = new();
    private long _statusVersion;

    private bool _reselect;
    private DateTime _phaseUntil;

    public DemoAccount(string id, string login, List<AbstractCampaign> catalogue, CampaignQueueService queue,
        CampaignStatusStore store, Action<DemoSetup> setup)
    {
        Id = id;
        Login = login;
        _queue = queue;
        _store = store;

        var options = new DemoSetup();
        setup(options);

        _offline = options.Offline;
        _linked = options.Linked;
        _unclaimed = options.Unclaimed;
        _campaigns = catalogue.Select(x => DemoCatalogue.ForAccount(x, _linked)).ToList();
        FavouriteGames = DemoCatalogue.Favourites;

        foreach (var (campaignId, minutes) in options.Progress)
        {
            _progress[campaignId] = minutes;
        }

        foreach (var (campaignId, kind, minutesAgo) in options.Notes)
        {
            _notes[campaignId] = new CampaignNote(kind, DateTime.UtcNow.AddMinutes(-minutesAgo));
        }

        foreach (var campaignId in options.Queue)
        {
            var campaign = _campaigns.First(x => x.Id == campaignId);
            _queue.Add(Id, new QueuedCampaign(campaign.Id, campaign.Name, campaign.Game?.DisplayName,
                campaign.Game?.BoxArtUrl, campaign.EndAt, DateTime.UtcNow.AddMinutes(-30)));
        }

        foreach (var line in options.Log)
        {
            Logs.Add(line.Level, line.Message);
        }

        CampaignsAt = DateTime.UtcNow.AddMinutes(-4);

        // As if earlier checks had asked live channels about these
        foreach (var (campaignId, minutesAgo) in options.Checked)
        {
            var status = StatusOf(_campaigns.First(x => x.Id == campaignId)) with
            {
                CheckedAt = DateTime.UtcNow.AddMinutes(-minutesAgo)
            };
            _checked[campaignId] = status;
            if (status.AllClaimed)
            {
                MarkDone(campaignId, _campaigns.First(x => x.Id == campaignId).EndAt);
            }
        }

        if (options.Watching is { } watching)
        {
            Watch(_campaigns.First(x => x.Id == watching), options.Channel);
        }
        else
        {
            Status = BotStatus.Idle;
            IdleReason = BotIdleReason.NothingToWatch;
            NextCycleAt = DateTime.UtcNow.AddSeconds(options.WaitSeconds);
        }
    }

    public LogBuffer Logs { get; } = new();

    public string? KnownBoxArt(string? gameId) => null;

    public IReadOnlyDictionary<string, CampaignStatus> CheckedStatus => _checked;

    public IReadOnlyDictionary<string, StatusAttempt> StatusAttempts => _attempts;

    public long StatusVersion => Interlocked.Read(ref _statusVersion);

    public bool IsKnownDone(string campaignId) => _store.IsDone(Id, campaignId);

    public void MarkDone(string campaignId, DateTime? endAt) => _store.MarkDone(Id, campaignId, endAt);

    public async Task<CampaignStatus?> CheckStatusAsync(string campaignId, TimeSpan maxAge,
        CancellationToken cancellationToken)
    {
        if (_checked.TryGetValue(campaignId, out var known) && DateTime.UtcNow - known.CheckedAt < maxAge)
        {
            return known;
        }

        var campaign = _campaigns.FirstOrDefault(x => x.Id == campaignId);
        if (campaign is not DropCampaign)
        {
            return known;
        }

        await Task.Delay(400, cancellationToken);
        if (_offline.Contains(campaignId))
        {
            _attempts[campaignId] = new StatusAttempt(DateTime.UtcNow, "No live channel to check right now");
            return known;
        }

        CampaignStatus status;
        lock (_lock)
        {
            status = StatusOf(campaign);
        }

        if (!_checked.TryGetValue(campaignId, out var previous) || !previous.Tiers.SequenceEqual(status.Tiers))
        {
            Interlocked.Increment(ref _statusVersion);
        }

        _checked[campaignId] = status;
        _attempts[campaignId] = new StatusAttempt(DateTime.UtcNow, null);
        if (status.AllClaimed)
        {
            MarkDone(campaignId, campaign.EndAt);
        }

        return status;
    }

    // What a live channel would report: minutes per drop, claimed unless the account can't receive it
    private CampaignStatus StatusOf(AbstractCampaign campaign)
    {
        var minutes = _progress.GetValueOrDefault(campaign.Id) ?? new int[campaign.TimeBasedDrops.Count];
        return new CampaignStatus(campaign.Id, campaign.TimeBasedDrops.Select((drop, i) => new TierStatus(
            drop.BenefitEdges[0].Benefit!.Name ?? drop.Name ?? "Drop",
            drop.BenefitEdges[0].Benefit!.ImageAssetURL,
            Math.Min(minutes[i], drop.RequiredMinutesWatched),
            drop.RequiredMinutesWatched,
            minutes[i] >= drop.RequiredMinutesWatched && !_unclaimed.Contains(campaign.Id))).ToList(),
            DateTime.UtcNow, "channel", DemoCatalogue.ChannelFor(campaign));
    }

    public string Id { get; }
    public string Login { get; }
    public BotStatus Status { get; private set; }
    public BotIdleReason IdleReason { get; private set; }
    public DateTime? NextCycleAt { get; private set; }
    public string? LastError => null;
    public DateTime? LastErrorAt => null;
    public AbstractCampaign? CurrentCampaign { get; private set; }
    public TimeBasedDrop? CurrentTimeBasedDrop { get; private set; }
    public User? CurrentBroadcaster { get; private set; }
    public int? CurrentMinutesWatched { get; private set; }
    public int? RequiredMinutesWatched { get; private set; }
    public IReadOnlyList<DropProgressInfo>? CurrentDropsProgress { get; private set; }
    public AbstractCampaign? CheckingCampaign { get; private set; }
    public IReadOnlyList<AbstractCampaign> Campaigns => _campaigns;
    public DateTime? CampaignsAt { get; private set; }
    public IReadOnlyList<string> FavouriteGames { get; }
    public bool OnlyFavouriteGames => true;
    public IReadOnlyList<string> AvoidedCampaigns { get; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, CampaignNote> Notes
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, CampaignNote>(_notes);
            }
        }
    }

    public Inventory? Inventory
    {
        get
        {
            lock (_lock)
            {
                return new Inventory
                {
                    DropCampaignsInProgress = _progress.Where(x => !StatusOf(_campaigns.First(c => c.Id == x.Key)).AllClaimed).Select(x =>
                    {
                        var campaign = _campaigns.First(c => c.Id == x.Key);
                        return new DropCampaign
                        {
                            Id = campaign.Id,
                            Name = campaign.Name,
                            Game = campaign.Game,
                            TimeBasedDrops = campaign.TimeBasedDrops.Select((drop, i) => new TimeBasedDrop
                            {
                                Id = drop.Id,
                                Name = drop.Name,
                                RequiredMinutesWatched = drop.RequiredMinutesWatched,
                                BenefitEdges = drop.BenefitEdges,
                                Self = new TimeBasedDropSelfEdge
                                {
                                    CurrentMinutesWatched = Math.Min(x.Value[i], drop.RequiredMinutesWatched),
                                    IsClaimed = x.Value[i] >= drop.RequiredMinutesWatched && !_unclaimed.Contains(x.Key)
                                }
                            }).ToList()
                        };
                    }).ToList()
                };
            }
        }
    }

    public Task RefreshCampaignsAsync(CancellationToken cancellationToken)
    {
        CampaignsAt = DateTime.UtcNow;
        Logs.Add(LogEventLevel.Information, $"{_campaigns.Count} campaigns fetched (demo)");
        return Task.Delay(600, cancellationToken);
    }

    public async Task<AbstractCampaign?> GetCampaignDetailsAsync(string campaignId,
        CancellationToken cancellationToken)
    {
        await Task.Delay(250, cancellationToken);
        return _campaigns.FirstOrDefault(x => x.Id == campaignId);
    }

    public void RequestReselect()
    {
        lock (_lock)
        {
            _reselect = true;
        }
    }

    // Every 3 seconds: a watched minute, or the next phase of a re-pick
    public void Step()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            if (_reselect)
            {
                _reselect = false;
                Status = BotStatus.Seeking;
                CheckingCampaign = NextCandidate();
                NextCycleAt = null;
                _phaseUntil = now.AddSeconds(5);
                Logs.Add(LogEventLevel.Information, "Re-pick requested from the web UI");
                if (CheckingCampaign is not null)
                {
                    Logs.Add(LogEventLevel.Information,
                        $"Checking {CheckingCampaign.Game?.DisplayName} ({CheckingCampaign.Name})...");
                }

                return;
            }

            switch (Status)
            {
                case BotStatus.Seeking when now >= _phaseUntil:
                    PickNext();
                    break;
                case BotStatus.Idle when NextCycleAt is { } next && now >= next:
                    Status = BotStatus.Seeking;
                    NextCycleAt = null;
                    CheckingCampaign = NextCandidate();
                    _phaseUntil = now.AddSeconds(5);
                    break;
                case BotStatus.Watching:
                    WatchMinute();
                    break;
            }
        }
    }

    private AbstractCampaign? NextCandidate()
    {
        var queued = _queue.GetCampaignIds(Id)
            .Select(id => _campaigns.FirstOrDefault(x => x.Id == id))
            .OfType<AbstractCampaign>();

        var normal = _campaigns
            .Where(x => x.Game?.DisplayName is { } game && FavouriteGames.Contains(game))
            .OrderBy(x => x.EndAt);

        return queued.Concat(normal).FirstOrDefault(x => x is DropCampaign && !IsDone(x) && !_offline.Contains(x.Id));
    }

    private void PickNext()
    {
        foreach (var queued in _queue.GetCampaignIds(Id))
        {
            if (_offline.Contains(queued))
            {
                _notes[queued] = new CampaignNote(CampaignNoteKind.NoLiveChannel, DateTime.UtcNow);
            }
        }

        var next = NextCandidate();
        if (next is null)
        {
            Status = BotStatus.Idle;
            IdleReason = BotIdleReason.NothingToWatch;
            NextCycleAt = DateTime.UtcNow.AddMinutes(5);
            CheckingCampaign = null;
            Logs.Add(LogEventLevel.Information, "No broadcaster or campaign left.");
            return;
        }

        Watch(next, null);
    }

    private void Watch(AbstractCampaign campaign, string? channel)
    {
        channel ??= DemoCatalogue.ChannelFor(campaign);
        CurrentCampaign = campaign;
        CurrentBroadcaster = new User { Id = "4" + Math.Abs(channel.GetHashCode() % 10000000), Login = channel.ToLowerInvariant(), DisplayName = channel };
        CheckingCampaign = null;
        NextCycleAt = null;
        Status = BotStatus.Watching;

        if (!_progress.ContainsKey(campaign.Id))
        {
            _progress[campaign.Id] = new int[campaign.TimeBasedDrops.Count];
        }

        UpdateWatchState();
        Logs.Add(LogEventLevel.Information,
            $"Current drop campaign: {campaign.Name} ({campaign.Game?.DisplayName}), watching {CurrentBroadcaster.Login} | {CurrentBroadcaster.Id}");
    }

    private void WatchMinute()
    {
        var campaign = CurrentCampaign!;
        var minutes = _progress[campaign.Id];
        var drops = campaign.TimeBasedDrops;

        for (var i = 0; i < drops.Count; i++)
        {
            if (minutes[i] < drops[i].RequiredMinutesWatched)
            {
                minutes[i]++;
                if (minutes[i] == drops[i].RequiredMinutesWatched)
                {
                    Logs.Add(LogEventLevel.Information,
                        $"Claimed {drops[i].BenefitEdges[0].Benefit!.Name} for {campaign.Name}");
                }
            }
        }

        UpdateWatchState();

        if (IsDone(campaign))
        {
            _notes[campaign.Id] = new CampaignNote(CampaignNoteKind.Completed, DateTime.UtcNow);
            Status = BotStatus.Seeking;
            IdleReason = BotIdleReason.CycleFinished;
            CheckingCampaign = NextCandidate();
            _phaseUntil = DateTime.UtcNow.AddSeconds(5);
            return;
        }

        Logs.Add(LogEventLevel.Information,
            $"Waiting 60 seconds... {CurrentMinutesWatched}/{RequiredMinutesWatched} minutes watched.");
    }

    private void UpdateWatchState()
    {
        var campaign = CurrentCampaign!;
        var minutes = _progress[campaign.Id];
        var drops = campaign.TimeBasedDrops;
        var active = Enumerable.Range(0, drops.Count).FirstOrDefault(i => minutes[i] < drops[i].RequiredMinutesWatched,
            drops.Count - 1);

        CurrentTimeBasedDrop = drops[active];
        CurrentMinutesWatched = minutes[active];
        RequiredMinutesWatched = drops[active].RequiredMinutesWatched;
        CurrentDropsProgress = drops.Select((drop, i) => new DropProgressInfo
        {
            Name = drop.BenefitEdges[0].Benefit!.Name ?? drop.Name ?? "Drop",
            CurrentProgress = Math.Min(minutes[i], drop.RequiredMinutesWatched),
            RequiredProgress = drop.RequiredMinutesWatched,
            IsClaimed = minutes[i] >= drop.RequiredMinutesWatched,
            IsActive = i == active,
            ImageUrl = drop.BenefitEdges[0].Benefit!.ImageAssetURL
        }).ToList();
    }

    private bool IsDone(AbstractCampaign campaign) =>
        _progress.TryGetValue(campaign.Id, out var minutes) &&
        campaign.TimeBasedDrops.Select((drop, i) => minutes[i] >= drop.RequiredMinutesWatched).All(x => x);
}

internal sealed class DemoSetup
{
    public string? Watching { get; set; }
    public string? Channel { get; set; }
    public int WaitSeconds { get; set; } = 220;
    public HashSet<string> Linked { get; } = new();
    public HashSet<string> Offline { get; } = new();
    public HashSet<string> Unclaimed { get; } = new();
    public List<(string CampaignId, int MinutesAgo)> Checked { get; } = new();
    public List<string> Queue { get; } = new();
    public Dictionary<string, int[]> Progress { get; } = new();
    public List<(string CampaignId, CampaignNoteKind Kind, int MinutesAgo)> Notes { get; } = new();
    public List<(LogEventLevel Level, string Message)> Log { get; } = new();
}

internal static class DemoCatalogue
{
    private const string BoxArt = "https://static-cdn.jtvnw.net/ttv-boxart/";

    public static readonly List<string> Favourites = new()
    {
        "Rainbow Six Siege", "World of Tanks", "Warframe", "SMITE 2", "Overwatch", "VALORANT",
        "The Elder Scrolls Online", "Genshin Impact", "EVE Online", "PAYDAY 3", "League of Legends",
        "World of Warships", "Hearthstone", "Minecraft", "Destiny 2", "Sea of Thieves", "Escape from Tarkov",
        "Tom Clancy's The Division 2", "REMATCH", "Aniimo", "War Thunder", "Guild Wars 2"
    };

    private static readonly Dictionary<string, (string Id, string Art)> Games = new()
    {
        ["Rainbow Six Siege"] = ("460630", "460630-144x192.jpg"),
        ["World of Tanks"] = ("27546", "27546_IGDB-144x192.jpg"),
        ["Warframe"] = ("66170", "66170_IGDB-144x192.jpg"),
        ["SMITE 2"] = ("2094865572", "2094865572-144x192.jpg"),
        ["Overwatch"] = ("515025", "515025-144x192.jpg"),
        ["VALORANT"] = ("516575", "516575-144x192.jpg"),
        ["The Elder Scrolls Online"] = ("65654", "65654_IGDB-144x192.jpg"),
        ["Genshin Impact"] = ("513181", "513181_IGDB-144x192.jpg"),
        ["EVE Online"] = ("13263", "13263-144x192.jpg"),
        ["PAYDAY 3"] = ("2113043143", "2113043143_IGDB-144x192.jpg"),
        ["League of Legends"] = ("21779", "21779-144x192.jpg"),
        ["World of Warships"] = ("32502", "32502_IGDB-144x192.jpg"),
        ["Rocket League"] = ("30921", "30921-144x192.jpg"),
        ["Apex Legends"] = ("511224", "511224-144x192.jpg"),
        ["Hearthstone"] = ("138585", "138585-144x192.jpg"),
        ["Minecraft"] = ("27471", "27471_IGDB-144x192.jpg"),
        ["Destiny 2"] = ("497057", "497057-144x192.jpg"),
        ["Sea of Thieves"] = ("490377", "490377-144x192.jpg"),
        ["Escape from Tarkov"] = ("491931", "491931_IGDB-144x192.jpg"),
        ["Marvel Rivals"] = ("1264310518", "1264310518_IGDB-144x192.jpg"),
        ["Tom Clancy's The Division 2"] = ("504463", "504463_IGDB-144x192.jpg"),
        ["REMATCH"] = ("1362102608", "1362102608_IGDB-144x192.jpg"),
        ["Aniimo"] = ("1267582829", "1267582829_IGDB-144x192.jpg"),
        ["War Thunder"] = ("66366", "66366_IGDB-144x192.jpg"),
        ["Guild Wars 2"] = ("19357", "19357_IGDB-144x192.jpg")
    };

    private static readonly Dictionary<string, string> Channels = new()
    {
        ["r6s-invitational"] = "Rainbow6",
        ["wot-finals"] = "WoT_Global",
        ["warframe-party"] = "Warframe",
        ["smite2-weekly"] = "SMITEGame",
        ["ow-s19"] = "PlayOverwatch",
        ["eso-summer"] = "ElderScrollsOnline",
        ["marvel-s4"] = "MarvelRivals",
        ["lol-worlds"] = "Riot Games",
        ["d2-revenant"] = "Bungie"
    };

    public static void FirstAccountSetup(DemoSetup setup)
    {
        setup.Watching = "r6s-invitational";
        setup.Channel = "Rainbow6";
        setup.Linked.UnionWith(new[]
        {
            "r6s-invitational", "wot-finals", "warframe-party", "smite2-weekly", "ow-s19", "valorant-vct",
            "eso-summer", "lol-worlds", "wows-naval", "aniimo-beta", "division2-y7", "d2-revenant"
        });
        setup.Offline.UnionWith(new[] { "warframe-party", "valorant-vct", "eve-weekly" });
        setup.Queue.AddRange(new[] { "warframe-party", "eso-summer" });
        setup.Progress["r6s-invitational"] = new[] { 60, 87, 87 };
        setup.Progress["wot-finals"] = new[] { 60, 120, 132 };
        setup.Progress["smite2-weekly"] = new[] { 120, 240, 360, 371, 371, 371, 371 };
        setup.Progress["ow-s19"] = new[] { 45, 45, 45, 45 };
        setup.Progress["payday3-masks"] = new[] { 60, 120, 180 };
        setup.Notes.Add(("warframe-party", CampaignNoteKind.NoLiveChannel, 3));
        setup.Notes.Add(("valorant-vct", CampaignNoteKind.NoLiveChannel, 3));
        setup.Notes.Add(("eve-weekly", CampaignNoteKind.NoLiveChannel, 4));
        setup.Notes.Add(("payday3-masks", CampaignNoteKind.Completed, 70));
        setup.Progress["eve-weekly"] = new[] { 60 };
        setup.Unclaimed.Add("eve-weekly");
        setup.Checked.Add(("payday3-masks", 12));
        setup.Checked.Add(("lol-worlds", 9));
        setup.Checked.Add(("aniimo-beta", 15));
        setup.Progress["aniimo-beta"] = new[] { 60, 120 };
        setup.Notes.Add(("genshin-61", CampaignNoteKind.NotEnoughTime, 70));
        setup.Log.AddRange(new (LogEventLevel, string)[]
        {
            (LogEventLevel.Information, "Removing 2 finished campaigns..."),
            (LogEventLevel.Information, "Checking Warframe (Watch Party)..."),
            (LogEventLevel.Information, "No live broadcaster found in this group of channels. (1/1)"),
            (LogEventLevel.Information, "No broadcaster found for this campaign."),
            (LogEventLevel.Information, "Checking The Elder Scrolls Online (Summer Event)..."),
            (LogEventLevel.Information, "Checking Rainbow Six Siege (Siege Invitational)..."),
            (LogEventLevel.Information, "Time based drops : Invitational Headgear"),
            (LogEventLevel.Debug, "Requesting stream segments every 10 seconds"),
            (LogEventLevel.Information, "Requested 18 new stream segments (0 failed, 0 failed polls)"),
            (LogEventLevel.Warning, "Failed to execute the query DropCurrentSessionContext (attempt 1/5)."),
            (LogEventLevel.Information, "Waiting 60 seconds... 86/120 minutes watched.")
        });
    }

    public static void SecondAccountSetup(DemoSetup setup)
    {
        setup.WaitSeconds = 220;
        setup.Linked.UnionWith(new[]
        {
            "r6s-invitational", "warframe-party", "ow-s19", "valorant-vct", "lol-worlds", "aniimo-beta",
            "marvel-s4", "division2-y7"
        });
        setup.Offline.UnionWith(new[] { "marvel-s4", "valorant-vct", "eve-weekly", "warframe-party" });
        setup.Queue.AddRange(new[] { "marvel-s4", "ow-s19" });
        setup.Progress["r6s-invitational"] = new[] { 60, 120, 240 };
        setup.Progress["ow-s19"] = new[] { 60, 120, 131, 131 };
        setup.Progress["eso-summer"] = new[] { 60, 61, 61 };
        setup.Progress["payday3-masks"] = new[] { 60, 120, 180 };
        setup.Progress["rematch-launch"] = new[] { 60 };
        setup.Notes.Add(("marvel-s4", CampaignNoteKind.NoLiveChannel, 2));
        setup.Notes.Add(("r6s-invitational", CampaignNoteKind.Completed, 300));
        setup.Notes.Add(("payday3-masks", CampaignNoteKind.Completed, 300));
        setup.Notes.Add(("rematch-launch", CampaignNoteKind.Completed, 260));
        setup.Progress["hs-masters"] = new[] { 60, 60 };
        setup.Unclaimed.Add("hs-masters");
        setup.Checked.Add(("rematch-launch", 20));
        setup.Log.AddRange(new (LogEventLevel, string)[]
        {
            (LogEventLevel.Information, "Removing 4 finished campaigns..."),
            (LogEventLevel.Information, "Checking Marvel Rivals (Season 4 Drops)..."),
            (LogEventLevel.Information, "No broadcaster found for this campaign."),
            (LogEventLevel.Information, "Checking Overwatch (Season 19 Drops)..."),
            (LogEventLevel.Information, "CurrentMinutesWatched > requiredMinutesWatched, skipping"),
            (LogEventLevel.Information, "No broadcaster or campaign left.")
        });
    }

    public static string ChannelFor(AbstractCampaign campaign) =>
        Channels.TryGetValue(campaign.Id, out var channel) ? channel : (campaign.Game?.DisplayName ?? "Streamer").Replace(" ", "") + "TV";

    public static List<AbstractCampaign> Build()
    {
        var now = DateTime.UtcNow;
        var campaigns = new List<AbstractCampaign>
        {
            Drop("r6s-invitational", "Siege Invitational", "Rainbow Six Siege", now.AddHours(53),
                ("Invitational Charm", 60), ("Invitational Headgear", 120), ("Invitational Uniform", 240)),
            Drop("wot-finals", "League Finals", "World of Tanks", now.AddHours(26),
                ("x3 Personal Reserves", 60), ("Premium Tank Rental", 120), ("250 Gold", 180)),
            Drop("warframe-party", "Watch Party", "Warframe", now.AddHours(13),
                ("Event Glyph", 30), ("500 Kuva", 60)),
            Drop("smite2-weekly", "Weekly Bundles", "SMITE 2", now.AddHours(70),
                ("Market Coins Bundle 1", 120), ("Market Coins Bundle 2", 240), ("Market Coins Bundle 3", 360),
                ("Market Coins Bundle 4", 480), ("Market Coins Bundle 5", 600), ("Market Coins Bundle 6", 720),
                ("Market Coins Bundle 7", 840)),
            Drop("ow-s19", "Season 19 Drops", "Overwatch", now.AddHours(98),
                ("Season 19 Spray", 60), ("Player Icon", 120), ("Name Card", 180), ("Legendary Skin", 240)),
            Drop("valorant-vct", "Champions Watch", "VALORANT", now.AddHours(118),
                ("Champions Spray", 60), ("Gun Buddy", 120)),
            Drop("eso-summer", "Summer Event", "The Elder Scrolls Online", now.AddHours(146),
                ("Treasure Map", 60), ("Event Hat", 120), ("Event Pet", 180)),
            Drop("genshin-61", "Version Livestream", "Genshin Impact", now.AddHours(30),
                ("Primogems x40", 30), ("Mora x20,000", 60), ("Hero's Wit x4", 120)),
            Drop("eve-weekly", "Capsuleer Weekly", "EVE Online", now.AddHours(22),
                ("Skill Points x25,000", 60)),
            Drop("payday3-masks", "Heist Masks", "PAYDAY 3", now.AddHours(170),
                ("Gold Mask", 60), ("Silver Mask", 120), ("Bronze Mask", 180)),
            Drop("lol-worlds", "Worlds 2026", "League of Legends", now.AddHours(480),
                ("Worlds Capsule", 60)),
            Drop("wows-naval", "Naval Legends", "World of Warships", now.AddHours(75),
                ("Premium Ship Camo", 60), ("Commander", 180)),
            Drop("rl-rlcs", "RLCS Worlds", "Rocket League", now.AddHours(190),
                ("RLCS Decal", 60), ("Goal Explosion", 240)),
            Drop("apex-algs", "ALGS Championship", "Apex Legends", now.AddHours(215),
                ("Holo-Spray", 60)),
            Drop("hs-masters", "Masters Tour", "Hearthstone", now.AddHours(50),
                ("Card Pack", 60), ("Card Back", 120)),
            Drop("mc-cape", "Cape Drop", "Minecraft", now.AddHours(119),
                ("Event Cape", 15)),
            Drop("d2-revenant", "Revenant Drops", "Destiny 2", now.AddHours(240),
                ("Emblem", 60), ("Shader", 120)),
            Drop("sot-s18", "Season 18", "Sea of Thieves", now.AddHours(285),
                ("Ship Flag", 60), ("Sails", 120)),
            Drop("eft-arena", "Tarkov Arena Drops", "Escape from Tarkov", now.AddHours(96),
                ("Weapon Case", 120)),
            Drop("marvel-s4", "Season 4 Drops", "Marvel Rivals", now.AddHours(160),
                ("Spray", 30), ("Nameplate", 90), ("Costume", 240)),
            Drop("division2-y7", "Year 7 Drops", "Tom Clancy's The Division 2", now.AddHours(140),
                ("Apparel Cache", 60), ("Exotic Cache", 120)),
            Drop("rematch-launch", "Launch Week", "REMATCH", now.AddHours(62),
                ("Kit", 60)),
            Drop("aniimo-beta", "Closed Beta", "Aniimo", now.AddHours(27),
                ("Beta Badge", 60), ("Partner Skin", 120)),
            new RewardCampaign
            {
                Id = "wt-night", Name = "Night Battles", Status = "ACTIVE",
                Game = Game("War Thunder"), EndsAt = now.AddHours(100), StartAt = now.AddDays(-3),
                UnlockRequirements = new QuestRewardUnlockRequirements { MinuteWatchedGoal = 240 },
                Rewards = new List<Reward> { new() { Id = "wt-night-1", Name = "Night Vision Decal" } }
            }
        };

        // Who needs an account link at all
        foreach (var campaign in campaigns.OfType<DropCampaign>())
        {
            campaign.AccountLinkURL = campaign.Id is "payday3-masks" or "rematch-launch" or "mc-cape"
                ? "https://www.twitch.tv/"
                : $"https://example.invalid/link/{campaign.Id}";
            campaign.DetailsURL = $"https://www.twitch.tv/drops/campaigns?dropID={campaign.Id}";
        }

        // A campaign that only counts on a few channels
        var valorant = campaigns.First(x => x.Id == "valorant-vct");
        valorant.Allow = new DropCampaignACL
        {
            IsEnabled = true,
            Channels = new[] { "valorant", "valorant_americas", "valorant_emea", "valorant_pacific" }
                .Select(x => new Channel { Name = x, DisplayName = x }).ToList()
        };

        return campaigns;
    }

    public static AbstractCampaign ForAccount(AbstractCampaign campaign, HashSet<string> linked)
    {
        if (campaign is not DropCampaign drop)
        {
            return campaign;
        }

        return new DropCampaign
        {
            Id = drop.Id, Name = drop.Name, Game = drop.Game, Status = drop.Status, StartAt = drop.StartAt,
            EndAt = drop.EndAt, TimeBasedDrops = drop.TimeBasedDrops, Allow = drop.Allow,
            AccountLinkURL = drop.AccountLinkURL, DetailsURL = drop.DetailsURL,
            Self = new DropCampaignSelfEdge { IsAccountConnected = linked.Contains(drop.Id) }
        };
    }

    private static Game Game(string name)
    {
        var (id, art) = Games[name];
        return new Game { Id = id, DisplayName = name, Name = name, BoxArtUrl = BoxArt + art };
    }

    private static DropCampaign Drop(string id, string name, string game, DateTime endAt,
        params (string Reward, int Minutes)[] tiers)
    {
        return new DropCampaign
        {
            Id = id,
            Name = name,
            Status = "ACTIVE",
            Game = Game(game),
            StartAt = endAt.AddDays(-10),
            EndAt = endAt,
            TimeBasedDrops = tiers.Select((tier, i) => new TimeBasedDrop
            {
                Id = $"{id}-{i}",
                Name = tier.Reward,
                RequiredMinutesWatched = tier.Minutes,
                StartAt = endAt.AddDays(-10),
                EndAt = endAt,
                BenefitEdges = new List<DropBenefitEdge>
                {
                    new() { Benefit = new DropBenefit { Id = $"{id}-b{i}", Name = tier.Reward } }
                }
            }).ToList()
        };
    }
}
