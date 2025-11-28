using Discord;
using Discord.Webhook;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

public class NotificationServices
{
    private readonly DiscordWebhookClient? _discordWebhookClient;
    
    public NotificationServices(BotSettings settings)
    {
        var url = settings.WebhookURL;
        _discordWebhookClient = string.IsNullOrEmpty(url) ? null : new DiscordWebhookClient(url);
    }
    
    public async Task SendNotification(BotUser user, string gameName, string itemName, string itemImage, string? code = null)
    {
        (Color color, string platformName) = user switch
        {
            TwitchUser => (new Color(0xA970FF), "Twitch"),
            KickUser   => (new Color(0x53FC18), "Kick"),
            _          => (new Color(0xFFFFFF), "Unknown")
        };

        Embed embed = new EmbedBuilder()
            .WithTitle($"{user.Login} received a new item for **{gameName}**!")
            .WithDescription($"**{itemName}** has been claimed{(code != null ? $" — Code: `{code}`" : string.Empty)}")
            .WithColor(color)
            .WithThumbnailUrl(itemImage)
            .WithFooter(platformName)
            .Build();
        
        await SendWebhookAsync(embed, itemImage);
    }

    public async Task SendNotification(BotUser user, string message, string itemImage, Uri actionUrl)
    {
        (Color color, string platformName) = user switch
        {
            TwitchUser => (new Color(0xA970FF), "Twitch"),
            KickUser   => (new Color(0x53FC18), "Kick"),
            _          => (new Color(0xFFFFFF), "Unknown")
        };

        Embed embed = new EmbedBuilder()
            .WithTitle($"{user.Login} recieve a new item!")
            .WithDescription(message)
            .WithColor(color)
            .WithThumbnailUrl(itemImage)
            .WithUrl(actionUrl.ToString())
            .WithFooter(platformName)
            .Build();
            
        
        await SendWebhookAsync(embed, itemImage);
    }

    public async Task SendErrorNotification(BotUser user, string title, string message, string itemImage)
    {
        (Color color, string platformName) = user switch
        {
            TwitchUser => (new Color(0xA970FF), "Twitch"),
            KickUser   => (new Color(0x53FC18), "Kick"),
            _          => (new Color(0xFFFFFF), "Unknown")
        };

        Embed embed = new EmbedBuilder()
            .WithTitle($"{user.Login} {title}")
            .WithDescription(message)
            .WithColor(color)
            .WithThumbnailUrl(itemImage)
            .WithFooter(platformName)
            .Build();
            
        
        await SendWebhookAsync(embed, itemImage);
    }

    public async Task SendErrorNotification(BotUser user, string title, string message)
    {
        (Color color, string platformName) = user switch
        {
            TwitchUser => (new Color(0xA970FF), "Twitch"),
            KickUser   => (new Color(0x53FC18), "Kick"),
            _          => (new Color(0xFFFFFF), "Unknown")
        };

        Embed embed = new EmbedBuilder()
            .WithTitle($"{user.Login} {title}")
            .WithDescription(message)
            .WithColor(color)
            .WithFooter(platformName)
            .Build();

        if (_discordWebhookClient is null) return;
        await _discordWebhookClient.SendMessageAsync(embeds: new[] { embed });
    }
    
    private async Task SendWebhookAsync(Embed embed, string avatar)
    {
        if (_discordWebhookClient is null) return;
        await _discordWebhookClient.SendMessageAsync(embeds: new[] { embed }, avatarUrl: avatar);
    }

}