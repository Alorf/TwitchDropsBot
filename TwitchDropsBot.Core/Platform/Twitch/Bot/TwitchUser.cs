using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using TwitchDropsBot.Core.Platform.Kick.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Factories.Bot;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Factories.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Models;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;
using TwitchDropsBot.Core.Platform.Twitch.Repository;
using TwitchDropsBot.Core.Platform.Twitch.Repository.Factory;
using TwitchDropsBot.Core.Platform.Twitch.Settings;
using TwitchDropsBot.Core.Platform.Twitch.WatchManager;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using WatchBrowser = TwitchDropsBot.Core.Platform.Twitch.WatchManager.WatchBrowser;
using WatchRequest = TwitchDropsBot.Core.Platform.Twitch.WatchManager.WatchRequest;

namespace TwitchDropsBot.Core.Platform.Twitch.Bot;

public class TwitchUser : BotUser
{
    private TwitchBot _baseBot;

    public string ClientSecret { get; set; }
    public string UniqueId { get; set; }
    public TwitchGqlRepository TwitchRepository { get; set; }
    private AbstractCampaign? _currentCampaign;
    public AbstractCampaign? CurrentCampaign
    {
        get => _currentCampaign;
        set
        {
            if (_currentCampaign != value)
            {
                _currentCampaign = value;
                OnPropertyChanged();
            }
        }
    }

    private TimeBasedDrop? _currentTimeBasedDrop;
    public TimeBasedDrop? CurrentTimeBasedDrop
    {
        get => _currentTimeBasedDrop;
        set
        {
            if (_currentTimeBasedDrop != value)
            {
                _currentTimeBasedDrop = value;
                OnPropertyChanged();
            }
        }
    }

    private User? _currentBroadcaster;
    public User? CurrentBroadcaster
    {
        get => _currentBroadcaster;
        set
        {
            if (_currentBroadcaster != value)
            {
                _currentBroadcaster = value;
                OnPropertyChanged();
            }
        }
    }

    private int? _currentMinutesWatched;
    public int? CurrentMinutesWatched
    {
        get => _currentMinutesWatched;
        set
        {
            if (_currentMinutesWatched != value)
            {
                _currentMinutesWatched = value;
                OnPropertyChanged();
            }
        }
    }

    private int? _requiredMinutesWatched;
    public int? RequiredMinutesWatched
    {
        get => _requiredMinutesWatched;
        set
        {
            if (_requiredMinutesWatched != value)
            {
                _requiredMinutesWatched = value;
                OnPropertyChanged();
            }
        }
    }
    
    private DropCurrentSession? _currentDropCurrentSession;
    public DropCurrentSession? CurrentDropCurrentSession
    {
        get => _currentDropCurrentSession;
        set
        {
            if (_currentDropCurrentSession != value)
            {
                _currentDropCurrentSession = value;
                OnPropertyChanged();
            }
        }
    }

    private Inventory? _inventory;
    public Inventory? Inventory
    {
        get => _inventory;
        set
        {
            if (_inventory != value)
            {
                _inventory = value;
                OnPropertyChanged();
            }
        }
    }
    
    public ITwitchWatchManager WatchManager { get; }

    public TwitchUser(
        TwitchUserSettings settings,
        IOptionsMonitor<BotSettings> BotSettings,
        ILogger logger,
        ITwitchRepositoryFactory repositoryFactory,
        ITwitchWatchManagerFactory factory,
        BotFactory botFactory,
        UISink? uiSink = null
        ) : base(settings, BotSettings, logger, uiSink)
    {
        ClientSecret = settings.ClientSecret;
        UniqueId = settings.UniqueId;
        OnlyConnectedAccounts = BotSettings.CurrentValue.TwitchSettings.OnlyConnectedAccounts;

        TwitchRepository = repositoryFactory.Create(this, logger);
        WatchManager = factory.Create(this);
        
        Logger.LogInformation($"WatchManager set to: {WatchManager.GetType().Name}");

        _baseBot = botFactory.CreateTwitchBot(this, logger);
    }

    public override async Task StartBot()
    {
        await _baseBot.StartBot();
    }

    public override void Close()
    {
        WatchManager.Close();
        Status = BotStatus.Idle;
        CurrentMinutesWatched = null;
        RequiredMinutesWatched = null;
    }
}