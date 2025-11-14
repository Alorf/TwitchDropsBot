using Microsoft.Extensions.Configuration;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Kick.Settings;
using TwitchDropsBot.Core.Platform.Kick.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;

namespace TwitchDropsBot.Core.Platform.Kick.Bot;

public class KickUser : BotUser
{
    public string? RefreshToken { get; set; }
    public string BearerToken { get; }
    
    public IKickWatchManager WatchManager { get; }
    public readonly KickHttpRepository KickRepository;

    
    public KickUser(KickUserSettings settings, UISink? uiSink = null) : base(settings, uiSink)
    {
        Logger = Logger.ForContext("UserType", this.GetType().Name).ForContext("User", this.Login);
        
        BearerToken = settings.BearerToken;
        
        KickRepository = new KickHttpRepository(this);
        
        var managerType = AppSettingsService.Settings.KickSettings.WatchManager;

        WatchManager = managerType switch
        {
            "WatchRequest" => new WatchRequest(KickRepository, Logger),
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

    public KickBot CreateBot()
    {
        return new KickBot(this, Logger);
    }
}

