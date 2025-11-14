using PuppeteerSharp;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;

namespace TwitchDropsBot.Core.Platform.Kick.WatchManager;

public class WatchBrowser : WatchBrowser<KickUser, Category, Channel>, IKickWatchManager
{
    public WatchBrowser(KickUser user) : base(user)
    {
    }

    public override async Task WatchStreamAsync(Channel streamer, Category category)
    {
        _disposed = false;

        var channel = await BotUser.KickRepository.GetChannelAsync(streamer.slug);
        
        if (channel?.Livestream is null)
        {
            throw new StreamOffline();
        }
        
        if (channel?.Livestream?.Category?.Contains(category, Category.IdComparer) == false)
        {
            throw new StreamOffline();
        }

        if (Page != null) return;

        Page = await BrowserService.Instance.AddUserAsync(BotUser);

        await Page.GoToAsync("https://www.kick.com/");

        var token = BotUser.BearerToken.Replace("Bearer ", "");
        token = Uri.EscapeDataString(token);

        await Page.SetCookieAsync(
            new CookieParam()
            {
                Name = "session_token",
                Value = token,
                Domain = "kick.com",
                Path = "/",
                Expires = DateTimeOffset.Now.AddDays(7).ToUnixTimeSeconds()
            });

        await Page.EvaluateExpressionAsync("sessionStorage.setItem('stream_quality', '160')");

        await Page.ReloadAsync();

        //Go to slug
        await Page.GoToAsync($"https://www.kick.com/{streamer.slug}");


        await Task.Delay(TimeSpan.FromSeconds(10));
    }

    public Task WatchStreamAsync(string broadcaster)
    {
        throw new NotImplementedException();
    }
}