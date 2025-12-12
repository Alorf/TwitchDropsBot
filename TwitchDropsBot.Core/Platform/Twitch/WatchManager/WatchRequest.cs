using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Models;
using TwitchDropsBot.Core.Platform.Twitch.Repository;
using Stream = TwitchDropsBot.Core.Platform.Twitch.Models.Stream;

namespace TwitchDropsBot.Core.Platform.Twitch.WatchManager;

public class WatchRequest : ITwitchWatchManager
{
    public TwitchUser BotUser { get; }
    private ILogger _logger;
    
    private string? streamUrl;
    private readonly TwitchGqlRepository twitchGraphQlClient;
    private DateTime lastRequestTime;
    private readonly bool enableOldSystem;
    private static string? _spadeUrl = null;
    private static DateTime _lastSpadeUrlFetch = DateTime.MinValue;
    private static readonly TimeSpan SpadeUrlRefreshInterval = TimeSpan.FromMinutes(30);
    private static readonly object SpadeUrlLock = new();
    
    public WatchRequest(TwitchUser user, ILogger logger, bool enableOldSystem)
    {
        BotUser = user;
        twitchGraphQlClient = BotUser.TwitchRepository;
        this.enableOldSystem = enableOldSystem;
        lastRequestTime = DateTime.MinValue;
        streamUrl = null;

        _logger = logger;
    }

    /*
     * Inspired by DevilXD's TwitchDropsMiner
     * https://github.dev/DevilXD/TwitchDropsMiner/blob/b20f98da7a72ddca20eb462229faf330026b3511/channel.py#L76
     */
    public async Task WatchStreamAsync(User broadcaster, Game game)
    {
        DateTime requestTime = DateTime.Now;
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Connection", "close");

        try
        {
            if (enableOldSystem)
            {
                if (streamUrl == null)
                {
                    PlaybackAccessToken? streamPlaybackAccessToken =
                        await twitchGraphQlClient.FetchPlaybackAccessTokenAsync(broadcaster.Login);

                    var requestBroadcastQualitiesURL =
                        $"https://usher.ttvnw.net/api/channel/hls/{broadcaster.Login}.m3u8?sig={streamPlaybackAccessToken!.Signature}&token={streamPlaybackAccessToken!.Value}";

                    HttpResponseMessage response = await client.GetAsync(requestBroadcastQualitiesURL);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    string[] lines = responseBody.Split("\n");
                    var regex = new Regex(@"VIDEO=""([^""]+)""");
                    var qualitiesPlaylist = new Dictionary<string, string>();
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("https"))
                        {
                            var previousLine = Array.IndexOf(lines, line) - 1;
                            var match = regex.Match(lines[previousLine]);
                            if (match.Success)
                            {
                                qualitiesPlaylist.Add(match.Groups[1].Value, line);
                            }
                        }
                    }

                    if (qualitiesPlaylist.TryGetValue("chunked", out var chunkedUrl))
                    {
                        streamUrl = chunkedUrl;
                    }
                    else
                    {
                        streamUrl = qualitiesPlaylist.Values.FirstOrDefault();
                    }
                }

                HttpResponseMessage response2 = await client.GetAsync(streamUrl);
                response2.EnsureSuccessStatusCode();
                string responseBody2 = await response2.Content.ReadAsStringAsync();

                string[] lines2 = responseBody2.Split("\n");
                string lastLine2 = lines2[lines2.Length - 2];

                HttpResponseMessage response3 = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, lastLine2));
                response3.EnsureSuccessStatusCode();
            }
            
            if ((requestTime - lastRequestTime).TotalSeconds >= 59)
            {
                var checkurl = await GetSpadeUrl();

                if (checkurl is null)
                {
                    throw new System.Exception("Failed to fetch Spade URL.");
                }

                var tempBroadcaster = await twitchGraphQlClient.FetchStreamInformationAsync(broadcaster.Login);

                if (tempBroadcaster is not null)
                {
                    if (tempBroadcaster.Stream is null)
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
            
                if (tempBroadcaster?.Stream is not null)
                {
                    var stream = tempBroadcaster.Stream;
                    var payload = GetPayload(tempBroadcaster, stream);

                    // Do post request to checkUrl, passing the payload
                    var request = new HttpRequestMessage(HttpMethod.Post, checkurl)
                    {
                        Content = new StringContent($"data={payload}", Encoding.UTF8, "application/x-www-form-urlencoded")
                    };
                
                    var payloadRequest = await client.SendAsync(request);
                    payloadRequest.EnsureSuccessStatusCode();
                }
                lastRequestTime = DateTime.Now;
            }
            
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public async Task<DropCurrentSession?> FakeWatchAsync(User broadcaster, Game game, int tryCount = 1)
    {
        _logger.LogDebug("Watching 20 seconds to ensure drops are registered...");
        
        for (int i = 0; i < tryCount; i++)
        {
            await WatchStreamAsync(broadcaster, game);
            await Task.Delay(TimeSpan.FromSeconds(20));
            Close();
        }

        return await BotUser.TwitchRepository.FetchCurrentSessionContextAsync(broadcaster);
    }

    public void Close()
    {
        streamUrl = null;
        lastRequestTime = DateTime.MinValue;
    }

    private string GetPayload(User broadcaster, Stream stream)
    {
        
        var payload = new[]
        {
            new Dictionary<string, object>
            {
                ["event"] = "minute-watched",
                ["properties"] = new Dictionary<string, object>
                {
                    ["broadcast_id"] = stream.Id,
                    ["channel_id"] = broadcaster.Id,
                    ["channel"] = broadcaster.Login,
                    ["hidden"] = false,
                    ["live"] = true,
                    ["location"] = "channel",
                    ["logged_in"] = true,
                    ["muted"] = false,
                    ["player"] = "site",
                    ["user_id"] = int.Parse(BotUser.Id),
                    ["device_id"] = BotUser.UniqueId,
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        return b64;
    }

    private async Task<string?> GetSpadeUrl()
    {
        lock (SpadeUrlLock)
        {
            if (_spadeUrl != null && (DateTime.Now - _lastSpadeUrlFetch) < SpadeUrlRefreshInterval)
                return _spadeUrl;
        }

        using var client = new HttpClient();
        var html = await client.GetStringAsync("https://www.twitch.tv");
        var regex = new Regex(@"https://assets\.twitch\.tv/config/settings\.[a-zA-Z0-9]+\.js");
        var match = regex.Match(html);
        if (!match.Success)
            return null;

        var assetUrl = match.Value;
        var jsContent = await client.GetStringAsync(assetUrl);
        var spadeRegex = new Regex(@"""beacon_url""\s*:\s*""([^""]+)""");
        var spadeMatch = spadeRegex.Match(jsContent);

        if (spadeMatch.Success)
        {
            lock (SpadeUrlLock)
            {
                _spadeUrl = spadeMatch.Groups[1].Value;
                _lastSpadeUrlFetch = DateTime.Now;
            }
            return _spadeUrl;
        }

        return null;
    }
}