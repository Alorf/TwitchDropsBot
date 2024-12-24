namespace TwitchDropsBot.Core.Object.TwitchGQL;

public class TimeBasedDrop : IInventorySystem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int RequiredMinutesWatched { get; set; }
    public int RequiredSubs { get; set; }
    public Self Self { get; set; }
    public Campaign Campaign { get; set; }
    public List<BenefitEdge> BenefitEdges { get; set; }
    public Game? Game { get; set; }
    public string? GetGameImageUrl()
    {
        return Game?.BoxArtURL;
    }
    public string? GetGameSlug()
    {
        return Game?.Slug;
    }

    public string GetGroup()
    {
        return Game?.DisplayName ?? Game?.Name ?? "Unknown";
    }


    public string GetImage()
    {
        return BenefitEdges[0].Benefit.ImageAssetURL;
    }

    public string GetName()
    {
        return BenefitEdges[0].Benefit.Name;
    }

    public string GetStatus()
    {
        return Self.IsClaimed ? "\u2714" : "\u26A0";
    }
}