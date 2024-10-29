namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class Game
{
    public string? Id { get; set; }
    public string? Slug { get; set; }
    public string DisplayName { get; set; }
    public string Name { get; set; }
    public string BoxArtURL { get; set; }
    public Stream Streams { get; set; }
        
    public bool IsFavorite { get; set; }
    
    public override string ToString()
    {
        return DisplayName;
    }
}