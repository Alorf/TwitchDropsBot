using Discord;
using Discord.Webhook;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Twitch.GraphQL;
using TwitchDropsBot.Core.Twitch.Models;
using TwitchDropsBot.Core.Twitch.Models.Custom;
using TwitchDropsBot.Core.WatchManager;

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
    public TwitchGraphQLClient TwitchGraphQlClient { get; set; }
    public List<string> FavouriteGames { get; set; }
    public List<string> PersonalFavouriteGames { get; set; }
    public bool OnlyFavouriteGames { get; set; }
    public bool OnlyConnectedAccounts { get; set; }
    public AbstractCampaign? CurrentCampaign { get; set; }
    public TimeBasedDrop? CurrentTimeBasedDrop { get; set; }
    public User? CurrentBroadcaster { get; set; }
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
    public WatchManager.WatchManager WatchManager;
    public CancellationTokenSource CancellationTokenSource { get; set; }
    private Inventory? _inventory;
    public Inventory? Inventory
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
    private DiscordWebhookClient? discordWebhookClient { get; set; }
    private string? _discordWebhookURl;
    public string? DiscordWebhookURl
    {
        get
        {
            return _discordWebhookURl;
        }
        set
        {
            if (value != null)
            {
                _discordWebhookURl = value;
                discordWebhookClient = new DiscordWebhookClient(_discordWebhookURl);
                //InitNotifications();
            }
        }
    }

    public TwitchUser(string login, string id, string clientSecret, string uniqueId, List<string> personalFavouriteGames)
    {
        var config = AppConfig.Instance;
        
        Login = login;
        Id = id;
        ClientSecret = clientSecret;
        UniqueId = uniqueId;
        PersonalFavouriteGames = personalFavouriteGames ?? new List<string>();
        
        TwitchGraphQlClient = new TwitchGraphQLClient(this);
        Logger = new Logger(this);
        Status = BotStatus.Idle;
        
        FavouriteGames = personalFavouriteGames is not null && personalFavouriteGames.Count > 0 ? personalFavouriteGames : config.FavouriteGames;
        OnlyFavouriteGames = config.OnlyFavouriteGames;
        OnlyConnectedAccounts = config.OnlyConnectedAccounts;

        string managerType = AppConfig.Instance.WatchManagerConfig.WatchManager;

        switch (managerType)
        {
            case "WatchRequest":
                WatchManager = new WatchRequest(this, CancellationTokenSource, false);
                break;
            case "WatchBrowser":
                WatchManager = new WatchBrowser(this, CancellationTokenSource);
                break;
            default:
                WatchManager = new WatchBrowser(this, CancellationTokenSource);
                break;
        }
        
        
        var managerTypeName = WatchManager.GetType().Name;
        
        Logger.Log($"WatchManager set to: {managerTypeName}");
    }

    private void InitNotifications()
    {
        Logger.OnLog += async (message) =>
            {
                await SendWebhookAsync(new List<Embed>
                {
                    new EmbedBuilder()
                        .WithTitle($"Log - {Login}")
                        .WithDescription(message)
                        .WithColor(Color.Green)
                        .Build()
                });
            };

            Logger.OnInfo += async (message) =>
            {
                await SendWebhookAsync(new List<Embed>
                {
                    new EmbedBuilder()
                        .WithTitle($"Info - {Login}")
                        .WithDescription(message)
                        .WithColor(Color.Gold)
                        .Build()
                });
            };

            Logger.OnError += async (message) =>
            {
                await SendWebhookAsync(new List<Embed>
                {
                    new EmbedBuilder()
                        .WithTitle($"Error - {Login}")
                        .WithDescription(message)
                        .WithColor(Color.Red)
                        .Build()
                });
            };

            Logger.OnException += async (exception) =>
            {
                await SendWebhookAsync(new List<Embed>
                {
                    new EmbedBuilder()
                        .WithTitle($"Error - {Login}")
                        .WithDescription(exception.ToString())
                        .WithColor(Color.Red)
                        .Build()
                });
            };
    }

    public async Task SendWebhookAsync(List<Embed> embeds, string? avatarUrl = null)
    {
        if (discordWebhookClient == null)
        {
            return;
        }

        foreach (var embed in embeds)
        {
            if (avatarUrl is null)
            {
                avatarUrl = embed.Thumbnail.ToString();
            }

            await discordWebhookClient.SendMessageAsync(embeds: new[] { embed }, avatarUrl: avatarUrl);
        }
    }
}