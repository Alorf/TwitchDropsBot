using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Webhook;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

public class NotificationService
{

    private readonly DiscordWebhookClient? _discordWebhookClient;
    private readonly IOptionsMonitor<BotSettings> _botSettings;
    private readonly ConcurrentDictionary<string, ulong> _progressMessages = new();

    public NotificationService(IOptionsMonitor<BotSettings> botSettings)
    {
        _botSettings = botSettings;
        var url = botSettings.CurrentValue.WebhookURL;
        _discordWebhookClient = string.IsNullOrEmpty(url) ? null : new DiscordWebhookClient(url);
    }

    public async Task SendNotification(BotUser user, string gameName, string itemName, string itemImage, string? code = null)
    {
        var (color, platformName) = GetPlatformData(user);
        var normalizedImage = NormalizeImageUrl(itemImage, platformName);

        var embedBuilder = new EmbedBuilder()
            .WithAuthor("TwitchDropsBot", url: "https://github.com/Alorf/TwitchDropsBot")
            .WithTitle($"{user.Login} received a new item for **{gameName}**!")
            .WithDescription($"**{itemName}** has been claimed{(code != null ? $" — Code: `{code}`" : string.Empty)}")
            .WithColor(color)
            .WithFooter(platformName);

        if (!string.IsNullOrEmpty(normalizedImage))
        {
            embedBuilder.WithThumbnailUrl(normalizedImage);
        }

        var embed = embedBuilder.Build();
        await SendWebhookAsync(embed, normalizedImage);
    }

    public async Task SendNotification(BotUser user, string message, string itemImage, Uri actionUrl)
    {
        var (color, platformName) = GetPlatformData(user);
        var normalizedImage = NormalizeImageUrl(itemImage, platformName);

        var embedBuilder = new EmbedBuilder()
            .WithAuthor("TwitchDropsBot", url: "https://github.com/Alorf/TwitchDropsBot")
            .WithTitle($"{user.Login} received a new item!")
            .WithDescription(message)
            .WithColor(color)
            .WithUrl(actionUrl.ToString())
            .WithFooter(platformName);

        if (!string.IsNullOrEmpty(normalizedImage))
        {
            embedBuilder.WithThumbnailUrl(normalizedImage);
        }

        var embed = embedBuilder.Build();
        await SendWebhookAsync(embed, normalizedImage);
    }

    public async Task SendErrorNotification(BotUser user, string title, string message, string? itemImage = null)
    {
        var (color, platformName) = GetPlatformData(user);
        var normalizedImage = NormalizeImageUrl(itemImage, platformName);

        var embedBuilder = new EmbedBuilder()
            .WithAuthor("TwitchDropsBot", url: "https://github.com/Alorf/TwitchDropsBot")
            .WithTitle($"{user.Login} {title}")
            .WithDescription(message)
            .WithColor(color)
            .WithFooter(platformName);

        if (!string.IsNullOrEmpty(normalizedImage))
        {
            embedBuilder.WithThumbnailUrl(normalizedImage);
        }

        var embed = embedBuilder.Build();

        if (_discordWebhookClient is null) return;
        await _discordWebhookClient.SendMessageAsync(embeds: new[] { embed });
    }

    public async Task SendClaimNotification(
        BotUser user,
        string gameName,
        string campaignName,
        string itemName,
        string itemImage,
        string uniqueKey,
        string? code = null,
        bool isLastDrop = false)
    {
        if (_discordWebhookClient is null) return;

        var (color, platformName) = GetPlatformData(user);
        var normalizedImage = NormalizeImageUrl(itemImage, platformName);

        var claimEmbedBuilder = new EmbedBuilder()
            .WithAuthor("TwitchDropsBot", url: "https://github.com/Alorf/TwitchDropsBot")
            .WithTitle($"🎉 {user.Login} claimed an item for **{gameName}**!")
            .WithDescription($"**{itemName}** has been claimed!{(code != null ? $" — Code: `{code}`" : string.Empty)}")
            .WithColor(color)
            .WithFooter($"{platformName} • Campaign: {campaignName}")
            .WithCurrentTimestamp();

        if (!string.IsNullOrEmpty(normalizedImage))
        {
            claimEmbedBuilder.WithThumbnailUrl(normalizedImage);
        }

        // If condensed notifications is true, we ONLY want to update the progress list later.
        // We DO NOT send a new claim message.
        // If it's the last drop, we modify the progress list to be the claim embed (since the campaign is finished).
        if (_botSettings.CurrentValue.CondensedNotifications)
        {
            if (isLastDrop && _progressMessages.TryGetValue(uniqueKey, out var prevMsgId))
            {
                try
                {
                    var condensedEmbed = new EmbedBuilder()
                        .WithTitle($"🎉 {user.Login} — {gameName} ✅ Completed")
                        .WithDescription($"The campaign **{campaignName}** is fully claimed!")
                        .WithColor(color)
                        .WithThumbnailUrl(normalizedImage)
                        .WithAuthor("TwitchDropsBot", url: "https://github.com/Alorf/TwitchDropsBot")
                        .WithCurrentTimestamp()
                        .Build();

                    await _discordWebhookClient.ModifyMessageAsync(prevMsgId, x =>
                    {
                        x.Embeds = new[] { condensedEmbed };
                    });
                    _progressMessages.TryRemove(uniqueKey, out _);
                }
                catch (Exception)
                {
                }
            }
            return;
        }

        var claimEmbed = claimEmbedBuilder.Build();

        // 1. Send a NEW message to trigger mobile push notification on Discord
        ulong newMsgId = 0;
        try
        {
            newMsgId = await _discordWebhookClient.SendMessageAsync(
                embeds: new[] { claimEmbed },
                avatarUrl: !string.IsNullOrEmpty(normalizedImage) ? normalizedImage : null);
        }
        catch (Exception)
        {
        }

        // 2. Modify or Delete previous progress message for this campaign (if exists)
        if (_progressMessages.TryGetValue(uniqueKey, out var previousMessageId))
        {
            try
            {
                if (isLastDrop)
                {
                    await _discordWebhookClient.DeleteMessageAsync(previousMessageId);
                    _progressMessages.TryRemove(uniqueKey, out _);
                }
                else
                {
                    await _discordWebhookClient.ModifyMessageAsync(previousMessageId, x =>
                    {
                        x.Embeds = new[] { claimEmbed };
                    });
                }
            }
            catch (Exception)
            {
            }
        }

        // 3. Set newly created message as active tracking message for this campaign
        if (newMsgId != 0 && !isLastDrop)
        {
            _progressMessages[uniqueKey] = newMsgId;
        }
    }

    public async Task SendOrUpdateProgressNotification(
        BotUser user,
        string gameName,
        string campaignName,
        string itemImage,
        List<DropProgressInfo> drops,
        string uniqueKey)
    {
        if (_discordWebhookClient is null) return;

        var (color, platformName) = GetPlatformData(user);

        var descriptionBuilder = new StringBuilder();
        descriptionBuilder.AppendLine($"**Campaign**: {campaignName}\n");

        foreach (var drop in drops)
        {
            if (drop.RequiredProgress <= 0 && !drop.IsClaimed)
            {
                descriptionBuilder.AppendLine($"**{drop.Name}** (🔒 Not watchable)");
                descriptionBuilder.AppendLine($"░░░░░░░░░░\n");
                continue;
            }

            var percentage = drop.RequiredProgress > 0 ? (drop.CurrentProgress / drop.RequiredProgress) : (drop.IsClaimed ? 1.0 : 0.0);
            var filledCount = Math.Clamp((int)Math.Round(percentage * 10), 0, 10);
            var progressBar = string.Concat(Enumerable.Repeat("█", filledCount)) + 
                              string.Concat(Enumerable.Repeat("░", 10 - filledCount));

            var statusStr = drop.IsClaimed 
                ? "✅ Claimed" 
                : (drop.IsActive ? $"⚡ In progress (`{percentage:P0}`)" : "⏳ Waiting");

            var displayProgress = Math.Min(drop.CurrentProgress, drop.RequiredProgress);
            descriptionBuilder.AppendLine($"**{drop.Name}** ({statusStr})");
            descriptionBuilder.AppendLine($"{progressBar} `{displayProgress:F0}/{(drop.RequiredProgress > 0 ? drop.RequiredProgress.ToString("F0") : "0")} min`\n");
        }

        var isCompleted = drops.Count > 0 && drops.All(d => d.IsClaimed);
        var title = isCompleted ? $"{user.Login} — {gameName} ✅ Completed" : $"{user.Login} — watching {gameName}";

        var embedBuilder = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(descriptionBuilder.ToString())
            .WithColor(color)
            .WithAuthor("TwitchDropsBot", url: "https://github.com/Alorf/TwitchDropsBot")
            .WithCurrentTimestamp();

        // Use the image of the drop currently being farmed if available
        var activeDrop = drops.FirstOrDefault(d => d.IsActive);
        var activeDropImage = !string.IsNullOrEmpty(activeDrop?.ImageUrl) ? activeDrop.ImageUrl : itemImage;
        var normalizedImage = NormalizeImageUrl(activeDropImage, platformName);

        if (!string.IsNullOrEmpty(normalizedImage))
        {
            embedBuilder.WithThumbnailUrl(normalizedImage);
        }

        var embed = embedBuilder.Build();

        if (_progressMessages.TryGetValue(uniqueKey, out var messageId))
        {
            try
            {
                await _discordWebhookClient.ModifyMessageAsync(messageId, x =>
                {
                    x.Embeds = new[] { embed };
                });
                return;
            }
            catch (Exception)
            {
                _progressMessages.TryRemove(uniqueKey, out _);
            }
        }

        try
        {
            var newId = await _discordWebhookClient.SendMessageAsync(
                embeds: new[] { embed }, 
                avatarUrl: !string.IsNullOrEmpty(normalizedImage) ? normalizedImage : null);
            
            _progressMessages[uniqueKey] = newId;
        }
        catch (Exception)
        {
        }
    }

    private string NormalizeImageUrl(string? url, string platformName)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        // If it already has a protocol, return as is
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        if (platformName == "Kick")
        {
            var trimmedUrl = url.TrimStart('/');
            return $"https://ext.cdn.kick.com/{trimmedUrl}";
        }

        // Try parsing as absolute, if not it's invalid
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            return string.Empty;
        }

        return url;
    }

    private (Color, string) GetPlatformData(BotUser user) => user switch
    {
        TwitchUser => (new Color(0xA970FF), "Twitch"),
        KickUser   => (new Color(0x53FC18), "Kick"),
        _          => (new Color(0xFFFFFF), "Unknown")
    };


    private async Task SendWebhookAsync(Embed embed, string avatar)
    {
        if (_discordWebhookClient is null) return;
        await _discordWebhookClient.SendMessageAsync(embeds: new[] { embed }, avatarUrl: !string.IsNullOrEmpty(avatar) ? avatar : null);
    }
}

public class DropProgressInfo
{
    public string Name { get; set; } = string.Empty;
    public double CurrentProgress { get; set; }
    public double RequiredProgress { get; set; }
    public bool IsClaimed { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
}
