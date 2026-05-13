using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Youtube.Factories.Repositories;
using TwitchDropsBot.Core.Platform.Youtube.Factories.WatchManager;
using TwitchDropsBot.Core.Platform.Youtube.Repository;
using TwitchDropsBot.Core.Platform.Youtube.Settings;
using TwitchDropsBot.Core.Platform.Youtube.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Factories.Bot;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Youtube.Bot;

public class YoutubeUser : BotUser
{
    private YoutubeBot _baseBot;
    public string Cookies { get; }

    public IYoutubeWatchManager WatchManager { get; }
    public readonly YoutubeHttpRepository YoutubeRepository;

    public YoutubeUser(
        YoutubeUserSettings settings,
        IOptionsMonitor<BotSettings> botSettings,
        ILogger logger,
        IYoutubeRepositoryFactory repositoryFactory,
        IYoutubeWatchManagerFactory factory,
        BotFactory botFactory,
        UISink? uiSink = null)
        : base(
            settings,
            botSettings,
            logger,
            uiSink
        )
    {
        Logger.LogTrace("Initializing YoutubeUser for login: {Login}", settings.Login);

        Cookies = settings.Cookies;
        if (!string.IsNullOrWhiteSpace(Cookies))
        {
            Logger.LogInformation("[YOUTUBE] Loaded inline cookies from user settings.");
        }
        else
        {
            Logger.LogWarning("[YOUTUBE] No inline cookies configured for {Login}", settings.Login);
        }

        YoutubeRepository = repositoryFactory.Create(this, logger);
        WatchManager = factory.Create(this);
        Logger.LogDebug("WatchManager set to: {ManagerType}", WatchManager.GetType().Name);

        _baseBot = botFactory.CreateYoutubeBot(this, logger);
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
            Logger.LogTrace("YoutubeUser closed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error closing YoutubeUser");
        }
    }
}
