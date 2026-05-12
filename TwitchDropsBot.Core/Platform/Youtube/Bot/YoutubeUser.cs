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

        var cookieFilePath = Path.Combine(AppContext.BaseDirectory, $"cookie-{settings.Login}.txt");
        if (File.Exists(cookieFilePath))
        {
            Cookies = File.ReadAllText(cookieFilePath);
            Logger.LogInformation("[YOUTUBE] Loaded cookies from {Path}", cookieFilePath);
        }
        else
        {
            Logger.LogWarning("[YOUTUBE] No cookie file found at {Path}", cookieFilePath);
            Cookies = null;
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