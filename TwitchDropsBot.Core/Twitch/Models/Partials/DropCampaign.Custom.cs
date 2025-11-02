
using System.Text.Json.Serialization;
using Discord;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Twitch.Models.Custom;

namespace TwitchDropsBot.Core.Twitch.Models;

public partial class DropCampaign : AbstractCampaign
{

    public override async Task NotifiateAsync(TwitchUser twitchUser)
    {
        TimeBasedDrop? timeBasedDrop = twitchUser.CurrentTimeBasedDrop;

        List<Embed> embeds = new List<Embed>();
        
        string? name = Game?.Name ?? Game?.DisplayName;

        Embed embed = new EmbedBuilder()
            .WithTitle($"{twitchUser.Login} recieve a new item for **{name ?? "Uknown Game"}**!")
            .WithDescription($"**{timeBasedDrop.GetName()}** have been claimed")
            .WithColor(new Color(2326507))
            .WithThumbnailUrl(timeBasedDrop.GetImage())
            //.WithUrl(action.Url)
            .Build();

        embeds.Add(embed);


        await twitchUser.SendWebhookAsync(embeds,
            timeBasedDrop.GetGameImageUrl(128) ?? timeBasedDrop.GetImage().ToString());
    }

    public override bool IsCompleted(Inventory inventory)
    {
        if (inventory.DropCampaignsInProgress.Any(x => x.Id == Id))
        {
            return false;
        }

        if (TimeBasedDrops?.Any() == true)
        {
            foreach (var timeBasedDrop in TimeBasedDrops)
            {
                foreach (var benefitEdge in timeBasedDrop.BenefitEdges)
                {
                    var correspondingDrop = inventory.GameEventDrops?
                        .FirstOrDefault(x => x.Id == benefitEdge.Benefit.Id);

                    benefitEdge.Benefit.IsClaimed = correspondingDrop != null
                                                    && correspondingDrop.LastAwardedAt >= timeBasedDrop.StartAt
                                                    && correspondingDrop.LastAwardedAt <= timeBasedDrop.EndAt;
                }
            }
        }
        else
        {
            throw new System.Exception("No TimeBasedDrops found in campaign " + Name);
        }


        var allTimeBasedDropsClaimed = TimeBasedDrops.All(drop => drop.IsClaimed());

        return allTimeBasedDropsClaimed;
    }
}