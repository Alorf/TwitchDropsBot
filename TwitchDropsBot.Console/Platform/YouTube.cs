using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.YouTube.Settings;

namespace TwitchDropsBot.Console.Platform;

public static class YouTube
{
    /// <summary>
    /// Interactively adds a YouTube user entry to the configuration file.
    /// The user's Google login happens inside the browser the first time the bot
    /// starts; here we only collect the login name and the channel IDs to watch.
    /// </summary>
    public static void AddYouTubeUserAsync(ILogger logger, SettingsManager manager)
    {
        logger.LogInformation("Enter a display name / login for this YouTube account (e.g. your Google account name):");
        var login = System.Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(login))
        {
            logger.LogError("Login cannot be empty.");
            return;
        }

        var settings = manager.Read();

        // Avoid duplicates by login name
        settings.YouTubeSettings.YouTubeUsers.RemoveAll(u =>
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase));

        var userSettings = new YouTubeUserSettings
        {
            Login   = login,
            Id      = login,   // YouTube has no simple numeric ID; use the login as ID
            Enabled = true,
        };

        settings.YouTubeSettings.YouTubeUsers.Add(userSettings);
        manager.Save(settings);

        logger.LogInformation("YouTube account '{Login}' added.", login);
        logger.LogInformation(
            "On the first run the bot will open a browser window for you to log in to Google. " +
            "Your session will be saved so you only need to do this once.");
    }
}
