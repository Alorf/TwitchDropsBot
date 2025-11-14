using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Kick.Settings;

public class KickUserSettings : BaseUserSettings
{
    // public string? AccessToken { get; set; }
    // public string? RefreshToken { get; set; }
    public string BearerToken { get; set; }
}