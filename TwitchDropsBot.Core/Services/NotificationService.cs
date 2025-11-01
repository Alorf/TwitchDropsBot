using Discord;
using Discord.Webhook;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;
using Game = TwitchDropsBot.Core.Object.TwitchGQL.Game;

namespace TwitchDropsBot.Core.Services;

public class NotificationService
{

    public static async Task SendWebhookAsync(TwitchUser twitchUser, TimeBasedDrop timeBasedDrop)
    {
        string login = twitchUser.Login;
        AbstractCampaign? campaign = timeBasedDrop.AbstractCampaign;

        if (campaign is null)
        {
            return;
        }

        List<Embed> embeds = new List<Embed>();

        string? name = campaign.Game?.Name ?? campaign.Game?.DisplayName;

        switch (timeBasedDrop.GetDistributionType())
        {
            case DistributionType.DIRECT_ENTITLEMENT:
            {
                var embed = new EmbedBuilder()
                    .WithTitle($"{login} receive a new item for **{name ?? "Unknown Game"}**!")
                    .WithDescription($"**{timeBasedDrop.GetName()}** have been claimed")
                    .WithColor(new Color(2326507))
                    .WithThumbnailUrl(timeBasedDrop.GetImage())
                    //.WithUrl(action.Url)
                    .Build();

                embeds.Add(embed);

                break;
            }
            case DistributionType.CODE:
            case DistributionType.BADGE:
            {
                var notifications = await twitchUser.GqlRequest.FetchNotificationsAsync(1);
                foreach (var edge in notifications.Edges)
                {
                    // Search for the first action with the type "click"
                    var action = edge.Node.Actions.FirstOrDefault(x => x.Type == "click");

                    var description = System.Net.WebUtility.HtmlDecode(edge.Node.Body);

                    var builder = new EmbedBuilder()
                        .WithTitle($"{twitchUser.Login} receive a new item!")
                        .WithDescription(description)
                        .WithColor(new Color(16766720))
                        .WithThumbnailUrl(edge.Node.ThumbnailURL);

                    if (action != null && !string.IsNullOrEmpty(action.Url))
                    {
                        builder.WithUrl(action.Url);
                    }

                    var embed = builder.Build();

                    embeds.Add(embed);
                }
                break;
            }
            default:
            {
                // Use notification center for unknown distribution type
                break;
            }
        }

        await SendWebhookAsync(embeds,
            timeBasedDrop.GetGameImageUrl(128) ?? timeBasedDrop.GetImage().ToString());
    }

    private static async Task SendWebhookAsync(List<Embed> embeds, string? avatarUrl = null)
    {
        string? discordWebhookURl = AppConfig.Instance.WebhookURL;

        if (discordWebhookURl is null)
        {
            return;
        }

        var discordWebhookClient = new DiscordWebhookClient(discordWebhookURl);

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