using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.YouTube.Factories.Repositories;
using TwitchDropsBot.Core.Platform.YouTube.Factories.WatchManager;
using TwitchDropsBot.Core.Platform.YouTube.Repository;
using TwitchDropsBot.Core.Platform.YouTube.Settings;
using TwitchDropsBot.Core.Platform.YouTube.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Factories.Bot;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.YouTube.Bot;

public class YouTubeUser : BotUser
{
    private readonly YouTubeBot _baseBot;

    public IYouTubeWatchManager  WatchManager    { get; }
    public YouTubeHttpRepository YouTubeRepository { get; }

    public YouTubeUser(
        YouTubeUserSettings settings,
        IOptionsMonitor<BotSettings> botSettings,
        ILogger logger,
        IYouTubeRepositoryFactory repositoryFactory,
        IYouTubeWatchManagerFactory watchManagerFactory,
        BotFactory botFactory,
        UISink? uiSink = null)
        : base(settings, botSettings, logger, uiSink)
    {
        Logger.LogTrace("Initializing YouTubeUser for login: {Login}", settings.Login);

        YouTubeRepository = repositoryFactory.Create(this, logger);
        WatchManager       = watchManagerFactory.Create(this);

        Logger.LogDebug("WatchManager set to: {ManagerType}", WatchManager.GetType().Name);

        _baseBot = botFactory.CreateYouTubeBot(this, logger);
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
            Logger.LogTrace("YouTubeUser closed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error closing YouTubeUser");
        }
    }
}
