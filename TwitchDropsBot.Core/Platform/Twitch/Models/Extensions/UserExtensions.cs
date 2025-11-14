namespace TwitchDropsBot.Core.Platform.Twitch.Models.Extensions;

public static class UserExtensions
{
    public static bool IsLive(this User user)
    {
        return user.Stream != null;
    }
}