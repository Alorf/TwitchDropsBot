using System.ComponentModel;
using System.Runtime.CompilerServices;
using Discord;
using Discord.Webhook;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TwitchDropsBot.Core.Platform.Shared.Bots;

public abstract class BotUser : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null!)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public UISink UISink { get; set; }
    public string Login { get; set; }
    public string Id { get; set; }
    public List<string> FavouriteGames { get; set; }
    public bool OnlyFavouriteGames { get; set; }
    public bool OnlyConnectedAccounts { get; set; }

    private BotStatus _status;
    public BotStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public ILogger Logger { get; }
    
    public CancellationTokenSource CancellationTokenSource { get; set; }
    private DiscordWebhookClient? _discordWebhookClient;

    protected BotUser(
        BaseUserSettings settings,
        IOptionsMonitor<BotSettings> BotSettings,
        ILogger logger,
        UISink? uiSink = null
        )
    {
        Login = settings.Login;
        Id = settings.Id;
        FavouriteGames = settings.FavouriteGames.Count > 0 
            ? settings.FavouriteGames 
            : BotSettings.CurrentValue.FavouriteGames;

        if (!string.IsNullOrEmpty(BotSettings.CurrentValue.WebhookURL))
        {
            _discordWebhookClient = new DiscordWebhookClient(BotSettings.CurrentValue.WebhookURL);
        }

        Status = BotStatus.Idle;
        OnlyFavouriteGames = BotSettings.CurrentValue.TwitchSettings.OnlyFavouriteGames;
        
        Logger = logger;

        if (uiSink != null)
        {
            UISink = uiSink;
            // loggerConfig.WriteTo.Sink(uiSink);
        }
    }

    public async Task SendWebhookAsync(List<Embed> embeds, string? avatarUrl = null)
    {
        if (_discordWebhookClient == null)
        {
            Logger.LogWarning("Discord webhook client not configured");
            return;
        }

        try
        {
            foreach (var embed in embeds)
            {
                avatarUrl ??= embed.Thumbnail?.Url;
                await _discordWebhookClient.SendMessageAsync(
                    embeds: new[] { embed }, 
                    avatarUrl: avatarUrl);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending Discord webhook");
        }
    }
    
    public abstract Task StartBot();

    public abstract void Close();
}