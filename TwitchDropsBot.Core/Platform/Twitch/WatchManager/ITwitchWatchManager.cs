using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models;

namespace TwitchDropsBot.Core.Platform.Twitch.WatchManager;

public interface ITwitchWatchManager : IWatchManager<TwitchUser, Game, User>
{
    public Task<DropCurrentSession?> FakeWatchAsync(User broadcaster, Game game, int tryCount = 3);
}