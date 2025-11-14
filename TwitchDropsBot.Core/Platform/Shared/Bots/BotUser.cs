using System.ComponentModel;
using System.Runtime.CompilerServices;
using Discord;
using Discord.Webhook;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Shared.Bots;

public abstract class BotUser: INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null!)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public UISink UISink;

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
    
    protected ILogger Logger;

    public CancellationTokenSource CancellationTokenSource { get; set; }
    public Action<string>? OnStatusChanged { get; set; }
    
    private DiscordWebhookClient? _discordWebhookClient;

    protected BotUser(BaseUserSettings settings, UISink? uiSink = null)
    {
        Login = settings.Login;
        Id = settings.Id;
        var config = AppSettingsService.Settings;
        FavouriteGames = settings.FavouriteGames.Count > 0
            ? settings.FavouriteGames
            : config.FavouriteGames;

        if (!string.IsNullOrEmpty(config.WebhookURL))
        {
            _discordWebhookClient = new DiscordWebhookClient(config.WebhookURL);    
        }
        
        Status = BotStatus.Idle;
        
        OnlyFavouriteGames = config.TwitchSettings.OnlyFavouriteGames;
        
        var appConfig = AppService.GetConfiguration();

        var loggerConfig = new LoggerConfiguration()
            .WriteTo.File($"logs/{Login}.txt")
            .ReadFrom.Configuration(appConfig);

        if (uiSink != null)
        {
            loggerConfig.WriteTo.Sink(uiSink);
        }

        Logger = loggerConfig.CreateLogger();
    }

    public async Task SendWebhookAsync(List<Embed> embeds, string? avatarUrl = null)
    {
        if (_discordWebhookClient == null)
        {
            return;
        }

        foreach (var embed in embeds)
        {
            if (avatarUrl is null)
            {
                avatarUrl = embed.Thumbnail.ToString();
            }

            await _discordWebhookClient.SendMessageAsync(embeds: new[] { embed }, avatarUrl: avatarUrl);
        }
    }

    public abstract void Close();
}