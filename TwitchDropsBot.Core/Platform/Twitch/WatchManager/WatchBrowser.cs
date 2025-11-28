using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models;

namespace TwitchDropsBot.Core.Platform.Twitch.WatchManager;

public class WatchBrowser : WatchBrowser<TwitchUser, Game, User>, ITwitchWatchManager
{
    public WatchBrowser(TwitchUser user, ILogger logger, BrowserService browserService) : base (user, logger, browserService)
    {
    }

    public override async Task WatchStreamAsync(User? broadcaster, Game game)
    {
        _disposed = false;
        // Check if stream still live, if not throw error and close
        if (broadcaster != null)
        {
            var tempBroadcaster = await BotUser.TwitchRepository.FetchStreamInformationAsync(broadcaster.Login);
            
            if (tempBroadcaster != null)
            {
                if (tempBroadcaster.Stream == null)
                {
                    throw new StreamOffline();
                }
                
                if (game.DisplayName != "Special Events")
                {
                    if (tempBroadcaster?.BroadcastSettings?.Game?.Id != game.Id)
                    {
                        throw new StreamOffline("Wrong game");
                    }   
                }
            }
        }
        
        if (Page != null) return;
        
        Page = await BrowserService.AddUserAsync(BotUser);
        
        await Page.GoToAsync("https://www.twitch.tv/");

        await Page.SetCookieAsync(
            new CookieParam()
            {
                Name = "auth-token",
                Value = BotUser.ClientSecret,
                Domain = ".twitch.tv",
                Path = "/",
                Expires = DateTimeOffset.Now.AddDays(7).ToUnixTimeSeconds()
            });

        // Some stream does not have 160p30
        await Page.EvaluateExpressionAsync("localStorage.setItem('video-quality', '{\"default\":\"360p30\"}')");

        await Page.ReloadAsync();

        await Page.GoToAsync($"https://www.twitch.tv/{broadcaster.Login}");

        // If classification overlay
        try
        {
            await Page.WaitForSelectorAsync("button[data-a-target='content-classification-gate-overlay-start-watching-button']", new() { Timeout = 10000 });
            await Page.ClickAsync("button[data-a-target='content-classification-gate-overlay-start-watching-button']");
        }
        catch (System.Exception ex)
        {
            Logger.LogInformation("[BROWSER] No classification button found, continuing...");
        }

        await Task.Delay(TimeSpan.FromSeconds(10));
    }

    public async Task<DropCurrentSession?> FakeWatchAsync(User broadcaster, Game game, int tryCount = 3)
    {
        // Watch for 20*trycount seconds
        var startTime = DateTime.Now;
        await WatchStreamAsync(broadcaster, game);

        while (true)
        {
            var timeElapsed = DateTime.Now - startTime;
            if (timeElapsed.TotalSeconds > 20 * tryCount)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Close();

        return await BotUser.TwitchRepository.FetchCurrentSessionContextAsync(broadcaster);
    }
}