using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Kick.Factories.Repositories;
using TwitchDropsBot.Core.Platform.Kick.Factories.WatchManager;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Kick.Settings;
using TwitchDropsBot.Core.Platform.Kick.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Factories.Bot;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Settings;

using TwitchDropsBot.Core.Platform.Kick.Models;

namespace TwitchDropsBot.Core.Platform.Kick.Bot;

public class KickUser : BotUser
{
    private KickBot _baseBot;
    public string BearerToken { get; }
    
    public IKickWatchManager WatchManager { get; }
    public readonly KickHttpRepository KickRepository;

    private Campaign? _currentCampaign;
    public Campaign? CurrentCampaign
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

    private Reward? _currentReward;
    public Reward? CurrentReward
    {
        get => _currentReward;
        set
        {
            if (_currentReward != value)
            {
                _currentReward = value;
                OnPropertyChanged();
            }
        }
    }

    private Channel? _currentBroadcaster;
    public Channel? CurrentBroadcaster
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

    private CampaignSummary? _currentSummary;
    public CampaignSummary? CurrentSummary
    {
        get => _currentSummary;
        set
        {
            if (_currentSummary != value)
            {
                _currentSummary = value;
                OnPropertyChanged();
            }
        }
    }

    public KickUser(
        KickUserSettings settings,
        IOptionsMonitor<BotSettings> botSettings,
        ILogger logger,
        IKickRepositoryFactory repositoryFactory,
        IKickWatchManagerFactory factory,
        BotFactory botFactory,
        UISink? uiSink = null)
        : base(
            settings,
            botSettings,
            logger,
            uiSink
        )
    {
        Logger.LogTrace("Initializing KickUser for login: {Login}", settings.Login);
        BearerToken = settings.BearerToken;
        KickRepository = repositoryFactory.Create(this, logger);
        WatchManager = factory.Create(this);
        Logger.LogDebug("WatchManager set to: {ManagerType}", WatchManager.GetType().Name);

        _baseBot = botFactory.CreateKickBot(this, logger);
    }

    public override async Task StartBot()
    {
        await _baseBot.StartBot();
    }

    public override void Close()
    {
        try
        {
            WatchManager.Close();
            Status = BotStatus.Idle;
            Logger.LogTrace("KickUser closed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error closing KickUser");
        }
    }
}