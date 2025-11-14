using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Models;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;
using TwitchDropsBot.Core.Platform.Twitch.Repository;
using TwitchDropsBot.Core.Platform.Twitch.Settings;
using TwitchDropsBot.Core.Platform.Twitch.WatchManager;

namespace TwitchDropsBot.Core.Platform.Twitch.Bot;

public class TwitchUser : BotUser
{
    public string ClientSecret { get; set; }
    public string UniqueId { get; set; }
    public TwitchGqlRepository TwitchRepository { get; set; }
    public AbstractCampaign? CurrentCampaign { get; set; }
    public TimeBasedDrop? CurrentTimeBasedDrop { get; set; }
    public User? CurrentBroadcaster { get; set; }
    
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

    public TwitchUser(TwitchUserSettings settings, UISink? uiSink = null) : base(settings, uiSink)
    {
        Logger = Logger.ForContext("UserType", this.GetType().Name).ForContext("User", this.Login);

        ClientSecret = settings.ClientSecret;
        UniqueId = settings.UniqueId;
        TwitchRepository = new TwitchGqlRepository(this, Logger);

        var managerType = AppSettingsService.Settings.TwitchSettings.WatchManager;
        
        OnlyConnectedAccounts = AppSettingsService.Settings.TwitchSettings.OnlyConnectedAccounts;

        WatchManager = managerType switch
        {
            "WatchRequest" => new WatchRequest(this, false),
            "WatchBrowser" => new WatchBrowser(this),
            _ => new WatchBrowser(this)
        };
        
        Logger.Information($"WatchManager set to: {WatchManager.GetType().Name}");
    }

    public override void Close()
    {
        WatchManager.Close();
        Status = BotStatus.Idle;
    }

    public TwitchBot CreateBot()
    {
        return new TwitchBot(this, Logger);
    }
}