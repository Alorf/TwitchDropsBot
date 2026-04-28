using System.IO.Compression;
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

                HttpResponseMessage response3 =
                    await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, lastLine2));
                response3.EnsureSuccessStatusCode();
            }

            if ((requestTime - lastRequestTime).TotalSeconds >= 59)
            {
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
                    var payload = GetPayload(tempBroadcaster, stream, game);

                    await twitchGraphQlClient.SimulateWatchAsync(payload);
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
        _logger.LogDebug("Watching {seconds} seconds to ensure drops are registered...", (20 * tryCount));

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

    private string GetPayload(User broadcaster, Stream stream, Game game)
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
                    ["client_time"] = DateTime.UtcNow.ToString("o").Replace("+00:00", "Z"),
                    ["game"] = game.Name ?? game.DisplayName ?? "",
                    ["game_id"] = game.Id ?? "",
                    ["hidden"] = false,
                    ["is_live"] = true,
                    ["live"] = true,
                    ["logged_in"] = true,
                    ["minutes_logged"] = 1,
                    ["muted"] = false,
                    ["user_id"] = int.Parse(BotUser.Id),
                }
            }
        };
        var json = JsonSerializer.Serialize(payload);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(jsonBytes, 0, jsonBytes.Length);
        }

        var compressedBytes = output.ToArray();
        var b64 = Convert.ToBase64String(compressedBytes);
        return b64;
    }
}