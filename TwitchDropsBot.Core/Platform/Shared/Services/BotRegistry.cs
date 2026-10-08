using TwitchDropsBot.Core.Platform.Shared.Bots;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

/// <summary>
/// The accounts this process runs, so the web UI can read their state.
/// </summary>
public class BotRegistry
{
    private readonly object _lock = new();
    private readonly List<BotUser> _users = new();

    public event Action<BotUser>? UserRegistered;

    public IReadOnlyList<BotUser> Users
    {
        get
        {
            lock (_lock)
            {
                return _users.ToList();
            }
        }
    }

    public void Register(BotUser user)
    {
        lock (_lock)
        {
            // A front-end that rebuilds an account replaces the old instance
            _users.RemoveAll(x => x.Id == user.Id && x.GetType() == user.GetType());
            _users.Add(user);
        }

        UserRegistered?.Invoke(user);
    }
}
