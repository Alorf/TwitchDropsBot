namespace TwitchDropsBot.Core.Twitch.Models.Interfaces;

public interface IInventorySystem
{
    public string Id { get; set; }
    public string GetName();
    public string GetImage();
    public string GetGroup();
    public string GetStatus();
    public string? GetGameImageUrl(int size);
    public string? GetGameSlug();

}