namespace TwitchDropsBot.Core.Object.TwitchGQL;

public abstract class AbstractBroadcaster
{
    public string Login { get; set; }
    public string Id { get; set; }
    public Stream? Stream { get; set; }
}