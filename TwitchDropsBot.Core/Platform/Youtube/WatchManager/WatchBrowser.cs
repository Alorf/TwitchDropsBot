using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Youtube.Bot;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;

namespace TwitchDropsBot.Core.Platform.Youtube.WatchManager;

public class WatchBrowser : WatchBrowser<YoutubeUser, string, string>, IYoutubeWatchManager
{
    public WatchBrowser(YoutubeUser user, ILogger logger, BrowserService browserService) : base(user, logger, browserService)
    {
    }

    public override async Task WatchStreamAsync(string streamUrl, string channelId)
    {
        _disposed = false;

        if (Page != null)
        {
            Logger.LogDebug("Already watching a stream, skipping navigation");
            return;
        }

        Page = await BrowserService.AddUserAsync(BotUser);
        Logger.LogInformation("Navigating to stream {StreamUrl} (channel {ChannelId})", streamUrl, channelId);

        await Page.GotoAsync(streamUrl);
        
        await Task.Delay(TimeSpan.FromSeconds(10));

        await Page.Keyboard.PressAsync("Space");

        await Task.Delay(TimeSpan.FromSeconds(10));
    }
}