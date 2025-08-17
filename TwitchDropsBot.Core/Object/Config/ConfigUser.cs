namespace TwitchDropsBot.Core.Object.Config;

public class ConfigUser
{
    public string Login { get; set; }
    public string Id { get; set; }
    public string ClientSecret { get; set; }
    public string UniqueId { get; set; }
    public bool Enabled { get; set; }
    public List<string> FavouriteGames { get; set; }
    
}