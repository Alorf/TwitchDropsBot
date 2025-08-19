using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.TwitchGQL;
using TwitchDropsBot.Core.Utilities;
using Stream = TwitchDropsBot.Core.Object.TwitchGQL.Stream;

namespace TwitchDropsBot.Core.WatchManager;

public class WatchRequest : WatchManager
{
    private string? streamURL;
    private GqlRequest gqlRequest;
    private DateTime lastRequestTime;
    private bool enableOldSystem;
    private static string? spadeUrl = null;
    private static DateTime lastSpadeUrlFetch = DateTime.MinValue;
    private static readonly TimeSpan spadeUrlRefreshInterval = TimeSpan.FromMinutes(30);
    private static readonly object spadeUrlLock = new();
    
    public WatchRequest(TwitchUser twitchUser, CancellationTokenSource cancellationTokenSource, bool enableOldSystem) : base(twitchUser, cancellationTokenSource)
    {
        gqlRequest = twitchUser.GqlRequest;
        this.enableOldSystem = enableOldSystem;
        lastRequestTime = DateTime.MinValue;
        streamURL = null;
    }

    /*
     * Inspired by DevilXD's TwitchDropsMiner
     * https://github.dev/DevilXD/TwitchDropsMiner/blob/b20f98da7a72ddca20eb462229faf330026b3511/channel.py#L76
     */
    public override async Task WatchStreamAsync(AbstractBroadcaster broadcaster)
    {
        DateTime requestTime = DateTime.Now;
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Connection", "close");

        cancellationTokenSource = new CancellationTokenSource();

        try
        {
            if (enableOldSystem)
            {
                if (streamURL == null)
                {
                    StreamPlaybackAccessToken? streamPlaybackAccessToken =
                        await gqlRequest.FetchPlaybackAccessTokenAsync(broadcaster.Login);

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
                        streamURL = chunkedUrl;
                    }
                    else
                    {
                        streamURL = qualitiesPlaylist.Values.FirstOrDefault();
                    }
                }

                HttpResponseMessage response2 = await client.GetAsync(streamURL);
                response2.EnsureSuccessStatusCode();
                string responseBody2 = await response2.Content.ReadAsStringAsync();

                string[] lines2 = responseBody2.Split("\n");
                string lastLine2 = lines2[lines2.Length - 2];

                HttpResponseMessage response3 = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, lastLine2));
                response3.EnsureSuccessStatusCode();
            }
            
            if ((requestTime - lastRequestTime).TotalSeconds >= 59)
            {
                var checkurl = await getSpadeUrl();

                if (checkurl is null)
                {
                    throw new System.Exception("Failed to fetch Spade URL.");
                }

                var tempBroadcaster = await gqlRequest.FetchStreamInformationAsync(broadcaster.Login);
            
                if (tempBroadcaster?.Stream != null)
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
            twitchUser.Logger.Error(ex);
            cancellationTokenSource.Cancel();
            throw;
        }
    }

    public override void Close()
    {
        streamURL = null;
        lastRequestTime = DateTime.MinValue;
    }

    private string GetPayload(AbstractBroadcaster broadcaster, Stream stream)
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
                    ["user_id"] = int.Parse(twitchUser.Id),
                    ["device_id"] = twitchUser.UniqueId,
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        return b64;
    }

    private async Task<string?> getSpadeUrl()
    {
        lock (spadeUrlLock)
        {
            if (spadeUrl != null && (DateTime.Now - lastSpadeUrlFetch) < spadeUrlRefreshInterval)
                return spadeUrl;
        }

        using var client = new HttpClient();
        var html = await client.GetStringAsync("https://www.twitch.tv");
        var regex = new Regex(@"https://assets\.twitch\.tv/config/settings\.[a-zA-Z0-9]+\.js");
        var match = regex.Match(html);
        if (!match.Success)
            return null;

        var assetUrl = match.Value;
        var jsContent = await client.GetStringAsync(assetUrl);
        var spadeRegex = new Regex(@"""spade_url""\s*:\s*""([^""]+)""");
        var spadeMatch = spadeRegex.Match(jsContent);

        if (spadeMatch.Success)
        {
            lock (spadeUrlLock)
            {
                spadeUrl = spadeMatch.Groups[1].Value;
                lastSpadeUrlFetch = DateTime.Now;
            }
            return spadeUrl;
        }

        return null;
    }
}