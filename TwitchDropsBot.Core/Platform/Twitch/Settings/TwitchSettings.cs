using TwitchDropsBot.Core.Platform.Shared.WatchManager;

namespace TwitchDropsBot.Core.Platform.Twitch.Settings;

public class TwitchSettings
{
    public List<TwitchUserSettings> TwitchUsers { get; set; } = new List<TwitchUserSettings>();
    public List<string> AvoidCampaign { get; set; } = new List<string>();
    public bool OnlyFavouriteGames { get; set; } = false;
    public bool MinimizeInTray { get; set; } = true;
    public bool ForceTryWithTags { get; set; } = false;
    public bool OnlyConnectedAccounts { get; set; } = false;
    public string WatchManager { get; set; } = WatchManagerType.WatchRequest;
}