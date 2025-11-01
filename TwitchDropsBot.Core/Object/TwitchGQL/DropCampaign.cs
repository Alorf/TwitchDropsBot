using Discord;

namespace TwitchDropsBot.Core.Object.TwitchGQL;

public class DropCampaign : AbstractCampaign
{
    public string ImageURL { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public Self Self { get; set; }

    // public Allow Allow { get; set; }
    // public List<TimeBasedDrop> TimeBasedDrops { get; set; }
    public Summary Summary { get; set; }
    public string AccountLinkURL { get; set; }

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
                                                    && correspondingDrop.lastAwardedAt >= timeBasedDrop.StartAt
                                                    && correspondingDrop.lastAwardedAt <= timeBasedDrop.EndAt;
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