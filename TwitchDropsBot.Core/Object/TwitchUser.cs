using System.Collections.ObjectModel;
using TwitchDropsBot.Core.Utilities;

namespace TwitchDropsBot.Core.Object;

public class TwitchUser
{
    public string Login { get; set; }
    public string Id { get; set; }
    public string ClientSecret { get; set; }
    public string UniqueId { get; set; }
    public GqlRequest GqlRequest { get; set; }
    public List<string> FavouriteGames { get; set; }
    public bool OnlyFavouriteGames { get; set; }
    public DropCampaign? CurrentDropCampaign { get; set; }
    public TimeBasedDrop? CurrentTimeBasedDrop { get; set; }
    public AbstractBroadcaster? CurrentBroadcaster { get; set; }
    public DropCurrentSession? CurrendDropCurrentSession { get; set; }
    public BotStatus Status { get; set; }
    public Logger Logger { get; set; }
    public string? StreamURL { get; set; }

    public TwitchUser(string login, string id, string clientSecret, string uniqueId)
    {
        Login = login;
        Id = id;
        ClientSecret = clientSecret;
        UniqueId = uniqueId;
        GqlRequest = new GqlRequest(this);
        Status = BotStatus.Idle;
        Logger = new Logger();
        Logger.TwitchUser = this;
        FavouriteGames = new List<string>();
        OnlyFavouriteGames = false;
        StreamURL = null;
    }

    /*
     * Inspired by DevilXD's TwitchDropsMiner
     * https://github.dev/DevilXD/TwitchDropsMiner/blob/b20f98da7a72ddca20eb462229faf330026b3511/channel.py#L76
     */
    public async Task WatchStreamAsync(string channelLogin)
    {
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Connection", "close");

        if (StreamURL == null)
        {
            StreamPlaybackAccessToken? streamPlaybackAccessToken =
                await GqlRequest.FetchPlaybackAccessTokenAsync(channelLogin);

            var requestBroadcastQualitiesURL =
                $"https://usher.ttvnw.net/api/channel/hls/{channelLogin}.m3u8?sig={streamPlaybackAccessToken!.Signature}&token={streamPlaybackAccessToken!.Value}";

            HttpResponseMessage response = await client.GetAsync(requestBroadcastQualitiesURL);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            // Split by \n and take the last line
            string[] lines = responseBody.Split("\n");
            StreamURL = lines[lines.Length - 1];
        }

        // Do get request to the playlist
        HttpResponseMessage response2 = await client.GetAsync(StreamURL);
        response2.EnsureSuccessStatusCode();
        string responseBody2 = await response2.Content.ReadAsStringAsync();

        // Split by \n and take the last line
        string[] lines2 = responseBody2.Split("\n");
        string lastLine2 = lines2[lines2.Length - 2];

        // Download the stream with head request
        HttpResponseMessage response3 = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, lastLine2));
        response3.EnsureSuccessStatusCode();
    }
}