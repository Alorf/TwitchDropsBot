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

namespace TwitchDropsBot.Core.Platform.Kick.Bot;

public class KickUser : BotUser
{
    private KickBot _baseBot;
    public string BearerToken { get; }
    
    public IKickWatchManager WatchManager { get; }
    public readonly KickHttpRepository KickRepository;

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