using Discord;

namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class RewardCampaignsAvailableToUser : AbstractCampaign
{
    public string? Brand { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? Summary { get; set; }
    public UnlockRequirements? UnlockRequirements { get; set; }
    public List<Reward>? Rewards { get; set; }
    public string DistributionType { get; set; }
    public override async Task NotifiateAsync(TwitchUser twitchUser)
    {
        // Todo: distinct badge rewards from code rewards
        var notifications = await twitchUser.GqlRequest.FetchNotificationsAsync(1);
        List<Embed> embeds = new List<Embed>();

        switch (DistributionType)
        {
            case "CODE":
                //Regex to find code
                var codeRegex = new System.Text.RegularExpressions.Regex(@"(?<=\*\*).+?(?=\*\*)", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                twitchUser.Logger.Log("Got code !");
                break;
            default:
                
                break;
        }

        foreach (var edge in notifications.Edges)
        {
            //Search for the first action with the type "click"
            var action = edge.Node.Actions.FirstOrDefault(x => x.Type == "click");

            var description = System.Net.WebUtility.HtmlDecode(edge.Node.Body);

            Embed embed = new EmbedBuilder()
                .WithTitle($"{twitchUser.Login} recieve a new item!")
                .WithDescription(edge.Node.Body)
                .WithColor(new Color(16766720))
                .WithThumbnailUrl(edge.Node.ThumbnailURL)
                .WithUrl(action.Url)
                .Build();

            embeds.Add(embed);
        }
        await twitchUser.SendWebhookAsync(embeds);

    }
}
