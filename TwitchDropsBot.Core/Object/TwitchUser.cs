using System.Collections.ObjectModel;
using System.ComponentModel;
using TwitchDropsBot.Core.Utilities;
using TwitchDropsBot.Core.Object.TwitchGQL;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;

namespace TwitchDropsBot.Core.Object;

public class TwitchUser : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null!)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Login { get; set; }
    public string Id { get; set; }
    public string ClientSecret { get; set; }
    public string UniqueId { get; set; }
    public GqlRequest GqlRequest { get; set; }
    public List<string> FavouriteGames { get; set; }
    public bool OnlyFavouriteGames { get; set; }
    public bool OnlyConnectedAccounts { get; set; }
    public AbstractCampaign? CurrentCampaign { get; set; }
    public TimeBasedDrop? CurrentTimeBasedDrop { get; set; }
    public AbstractBroadcaster? CurrentBroadcaster { get; set; }
    private DropCurrentSession? _currentDropCurrentSession;
    public DropCurrentSession? CurrentDropCurrentSession
    {
        get => this._currentDropCurrentSession;
        set
        {
            if (_currentDropCurrentSession != value)
            {
                _currentDropCurrentSession = value;
                OnPropertyChanged();
            }
        }
    }
    private BotStatus _status;
    public BotStatus Status
    {
        get => this._status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }
    public Logger Logger { get; set; }
    public string? StreamURL { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    private Inventory _inventory;
    public Inventory Inventory
    {
        get => this._inventory;
        set
        {
            if (_inventory != value)
            {
                _inventory = value;
                OnPropertyChanged();
            }
        }
    }

    public Action<string>? OnStatusChanged { get; set; }

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