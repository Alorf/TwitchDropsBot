using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;

namespace TwitchDropsBot.Core.Platform.Kick.WatchManager;

public class WatchBrowser : WatchBrowser<KickUser, Category, Channel>, IKickWatchManager
{
    public WatchBrowser(KickUser user, ILogger logger, BrowserService browserService) : base(user, logger, browserService)
    {
    }

    public override async Task WatchStreamAsync(Channel streamer, Category category)
    {
        _disposed = false;

        var channel = await BotUser.KickRepository.GetChannelAsync(streamer.slug);

        if (channel?.Livestream is null)
            throw new StreamOffline();

        if (channel.Livestream.Category?.Contains(category) == false)
            throw new StreamOffline();

        if (Page != null) return;

        Page = await BrowserService.AddUserAsync(BotUser);

        await Page.GotoAsync("https://www.kick.com/");

        var token = Uri.EscapeDataString(BotUser.BearerToken.Replace("Bearer ", ""));

        await Page.Context.AddCookiesAsync(
        [
            new Cookie
            {
                Name    = "session_token",
                Value   = token,
                Domain  = "kick.com",
                Path    = "/",
                Expires = DateTimeOffset.Now.AddDays(7).ToUnixTimeSeconds()
            }
        ]);

        await Page.EvaluateAsync("sessionStorage.setItem('stream_quality', '160')");

        await Page.ReloadAsync();

        await Page.GotoAsync($"https://www.kick.com/{streamer.slug}");

        await Task.Delay(TimeSpan.FromSeconds(10));
    }
}