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

    public List<Channel>? GetChannels()
    {
        return Allow?.Channels;
    }

    public override async Task NotifiateAsync(TwitchUser twitchUser)
    {
        TimeBasedDrop? timeBasedDrop = twitchUser.CurrentTimeBasedDrop;

        List<Embed> embeds = new List<Embed>();

        Embed embed = new EmbedBuilder()
            .WithTitle($"{twitchUser.Login} recieve a new item for **{Game?.DisplayName}**!")
            .WithDescription($"**{timeBasedDrop.GetName()}** have been claimed")
            .WithColor(new Color(2326507))
            .WithThumbnailUrl(timeBasedDrop.GetImage())
            //.WithUrl(action.Url)
            .Build();

        embeds.Add(embed);


        await twitchUser.SendWebhookAsync(embeds, timeBasedDrop.GetGameImageUrl(128) ?? timeBasedDrop.GetImage().ToString());
    }
}