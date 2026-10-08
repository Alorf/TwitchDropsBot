using System.Net;
using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Twitch.Repository;
using TwitchDropsBot.Core.Platform.Twitch.Utils;

namespace TwitchDropsBot.Core.Platform.Twitch.WatchManager;

/*
 * Since 2026-10-07 Twitch credits Drops progress only while the stream's media segments are being
 * requested, the way a player does; minute-watched events alone no longer count. This polls the
 * stream's HLS playlist every 10 seconds and sends a HEAD request for every segment it has not
 * requested yet, so no audio or video is downloaded.
 * Same approach as rangermix/TwitchDropsMiner#164.
 */
public sealed class HlsSegmentWatcher : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PlaylistTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SegmentTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ResolveRetryDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMinutes(5);
    private const int SeenSegmentLimit = 256;

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly TwitchGqlRepository _repository;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly HashSet<string> _seenSegments = new();
    private readonly Queue<string> _seenOrder = new();
    private Uri? _mediaPlaylist;
    private DateTime _nextResolveAttempt = DateTime.MinValue;
    private long _lastTouchTicks;
    private int _segmentsRequested;
    private int _segmentsFailed;
    private int _pollsFailed;
    private bool _reportedStall;

    public string Login { get; }

    public bool IsRunning => !_loop.IsCompleted;

    public HlsSegmentWatcher(string login, TwitchGqlRepository repository, ILogger logger)
    {
        Login = login;
        _repository = repository;
        _logger = logger;
        Touch();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Touch()
    {
        Interlocked.Exchange(ref _lastTouchTicks, DateTime.UtcNow.Ticks);
    }

    public void Dispose()
    {
        _cts.Cancel();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogDebug("Requesting stream segments every {Seconds} seconds", PollInterval.TotalSeconds);
        var nextReport = DateTime.UtcNow + TimeSpan.FromMinutes(1);

        while (!ct.IsCancellationRequested)
        {
            // Close() stops this, but never outlive the watch loop if a path forgets to.
            if (DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastTouchTicks)) > IdleLimit)
            {
                _logger.LogDebug("Stopped requesting stream segments, nothing is watching the stream");
                return;
            }

            var started = DateTime.UtcNow;

            try
            {
                if (!await PollAsync(ct))
                {
                    _pollsFailed++;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Only the type: messages can carry the signed playlist URL.
                _pollsFailed++;
                _logger.LogDebug("Stream segment poll failed ({Error})", ex.GetType().Name);
            }

            if (DateTime.UtcNow >= nextReport)
            {
                Report();
                nextReport = DateTime.UtcNow + ReportInterval;
            }

            var wait = PollInterval - (DateTime.UtcNow - started);

            if (wait > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(wait, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task<bool> PollAsync(CancellationToken ct)
    {
        if (_mediaPlaylist is null && DateTime.UtcNow >= _nextResolveAttempt)
        {
            // At most once a minute: the token query logs an error for every failed attempt.
            _nextResolveAttempt = DateTime.UtcNow + ResolveRetryDelay;
            _mediaPlaylist = await FetchMediaPlaylistUrlAsync(ct);
        }

        var playlistUrl = _mediaPlaylist;

        if (playlistUrl is null)
        {
            return false;
        }

        var (status, playlist) = await SendAsync(HttpMethod.Get, playlistUrl, PlaylistTimeout, ct);

        if (status != HttpStatusCode.OK)
        {
            ForgetPlaylistIfExpired(status);
            return false;
        }

        var segments = ParsePlaylist(playlist, playlistUrl, false);

        if (segments.Count == 0)
        {
            return false;
        }

        var succeeded = true;

        foreach (var segment in segments)
        {
            var key = segment.AbsoluteUri;

            if (_seenSegments.Contains(key))
            {
                continue;
            }

            HttpStatusCode? segmentStatus = null;

            try
            {
                var (headStatus, _) = await SendAsync(HttpMethod.Head, segment, SegmentTimeout, ct);
                segmentStatus = headStatus;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timed out: retried on the next poll while the playlist still lists it.
            }
            catch (HttpRequestException)
            {
            }

            if (segmentStatus == HttpStatusCode.OK)
            {
                Remember(key);
                _segmentsRequested++;
                continue;
            }

            succeeded = false;
            _segmentsFailed++;

            if (segmentStatus is { } failedStatus)
            {
                ForgetPlaylistIfExpired(failedStatus);
            }
        }

        return succeeded;
    }

    private async Task<Uri?> FetchMediaPlaylistUrlAsync(CancellationToken ct)
    {
        var token = await _repository.FetchPlaybackAccessTokenAsync(Login);

        if (string.IsNullOrEmpty(token?.Value) || string.IsNullOrEmpty(token.Signature))
        {
            return null;
        }

        var masterUrl = new Uri(
            $"https://usher.ttvnw.net/api/channel/hls/{Uri.EscapeDataString(Login)}.m3u8" +
            $"?sig={Uri.EscapeDataString(token.Signature)}&token={Uri.EscapeDataString(token.Value)}");

        var (status, master) = await SendAsync(HttpMethod.Get, masterUrl, PlaylistTimeout, ct);

        if (status != HttpStatusCode.OK)
        {
            return null;
        }

        // The last variant is normally audio only or the lowest bitrate; only HEAD requests follow anyway.
        var variants = ParsePlaylist(master, masterUrl, true);
        return variants.Count > 0 ? variants[^1] : null;
    }

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpMethod method, Uri url,
        TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        using var request = new HttpRequestMessage(method, url);

        if (method == HttpMethod.Get)
        {
            // The CDN drops the connection shortly after serving a playlist anyway.
            request.Headers.ConnectionClose = true;
        }

        using var response =
            await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

        var body = method == HttpMethod.Get
            ? await response.Content.ReadAsStringAsync(timeoutCts.Token)
            : string.Empty;

        return (response.StatusCode, body);
    }

    // URI lines of a master (#EXT-X-STREAM-INF) or media (#EXTINF) playlist; anything unexpected yields none.
    public static List<Uri> ParsePlaylist(string playlist, Uri baseUrl, bool master)
    {
        var urls = new List<Uri>();

        if (!playlist.TrimStart().StartsWith("#EXTM3U", StringComparison.Ordinal))
        {
            return urls;
        }

        var tag = master ? "#EXT-X-STREAM-INF:" : "#EXTINF:";
        var pending = false;

        foreach (var rawLine in playlist.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('#'))
            {
                pending |= line.StartsWith(tag, StringComparison.Ordinal);
                continue;
            }

            if (!pending
                || !Uri.TryCreate(baseUrl, line, out var url)
                || url.Scheme is not ("http" or "https")
                || (master && !url.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)))
            {
                return new List<Uri>();
            }

            urls.Add(url);
            pending = false;
        }

        return pending ? new List<Uri>() : urls;
    }

    private void ForgetPlaylistIfExpired(HttpStatusCode status)
    {
        // Signed playlist URLs expire, and a restarted stream gets new ones.
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound
            or HttpStatusCode.Gone)
        {
            _mediaPlaylist = null;
        }
    }

    private void Remember(string key)
    {
        if (!_seenSegments.Add(key))
        {
            return;
        }

        _seenOrder.Enqueue(key);

        while (_seenOrder.Count > SeenSegmentLimit)
        {
            _seenSegments.Remove(_seenOrder.Dequeue());
        }
    }

    private void Report()
    {
        if (_segmentsRequested > 0)
        {
            if (_reportedStall)
            {
                _logger.LogInformation("Stream segment requests are working again");
                _reportedStall = false;
            }

            _logger.LogDebug("Requested {Requested} new stream segments ({Failed} failed, {Polls} failed polls)",
                _segmentsRequested, _segmentsFailed, _pollsFailed);
        }
        else if (!_reportedStall)
        {
            _logger.LogWarning(
                "No stream segment could be requested ({Failed} failed, {Polls} failed polls), so Twitch will not count this watch time",
                _segmentsFailed, _pollsFailed);
            _reportedStall = true;
        }

        _segmentsRequested = 0;
        _segmentsFailed = 0;
        _pollsFailed = 0;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All
        });

        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Constant.TwitchDevice.UserAgents[0]);
        return client;
    }
}
