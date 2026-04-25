using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.YouTube.Bot;
using TwitchDropsBot.Core.Platform.YouTube.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.YouTube.Factories.WatchManager;

public class YouTubeWatchManagerFactory : IYouTubeWatchManagerFactory
{
    private readonly IOptionsMonitor<BotSettings> _botSettings;

    public YouTubeWatchManagerFactory(IOptionsMonitor<BotSettings> botSettings)
    {
        _botSettings = botSettings;
    }

    public IYouTubeWatchManager Create(YouTubeUser user)
    {
        return new YouTubeWatchBrowser(user, user.Logger, _botSettings);
    }
}
