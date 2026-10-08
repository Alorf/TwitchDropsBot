using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace TwitchDropsBot.Web.Api;

/// <summary>Password check plus a per-address limit on failed logins.</summary>
public sealed class LoginThrottle
{
    private const int MaxFailures = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly byte[]? _passwordHash;
    private readonly ConcurrentDictionary<string, (int Failures, DateTime WindowStart)> _failures = new();

    public LoginThrottle(WebUiOptions options)
    {
        _passwordHash = options.Password is null ? null : SHA256.HashData(Encoding.UTF8.GetBytes(options.Password));
    }

    public bool IsBlocked(string client)
    {
        return _failures.TryGetValue(client, out var entry) &&
               DateTime.UtcNow - entry.WindowStart < Window &&
               entry.Failures >= MaxFailures;
    }

    public bool Check(string client, string? password)
    {
        var ok = _passwordHash is not null && password is not null &&
                 CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(password)),
                     _passwordHash);

        if (ok)
        {
            _failures.TryRemove(client, out _);
            return true;
        }

        _failures.AddOrUpdate(client,
            _ => (1, DateTime.UtcNow),
            (_, entry) => DateTime.UtcNow - entry.WindowStart < Window
                ? (entry.Failures + 1, entry.WindowStart)
                : (1, DateTime.UtcNow));
        return false;
    }
}
