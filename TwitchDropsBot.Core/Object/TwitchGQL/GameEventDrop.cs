namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class GameEventDrop : IInventorySystem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Game? Game { get; set; }
    public string? ImageURL { get; set; }
    public bool IsConnected { get; set; }
    public int? TotalCount { get; set; }
    public DateTime lastAwardedAt { get; set; }

    public string? GetGameImageUrl(int size)
    {
        return null;
    }

    public string? GetGameSlug()
    {
        return "inventory";
    }

    public string GetGroup()
    {
        return "Inventory";
    }

    public string GetImage()
    {
        return ImageURL;
    }

    public string GetName()
    {
        return Name;
    }

    public string GetStatus()
    {
        return IsConnected ? "\u2714" : "\u26A0";
    }
}