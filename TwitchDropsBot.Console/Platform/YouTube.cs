using Microsoft.Extensions.Logging;
using TwitchDropsBot.Console.Utils;
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

        logger.LogInformation("Use cookie login for this YouTube account? (Y/N)");
        bool cookieLogin;
        try
        {
            cookieLogin = UserInput.ReadInput(["y", "n"]) == "y";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invalid input for CookieLogin.");
            return;
        }

        string? cookiesFilePath = null;
        if (cookieLogin)
        {
            logger.LogInformation("Enter the absolute path to your YouTube cookies file (Netscape format):");
            cookiesFilePath = System.Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(cookiesFilePath))
            {
                logger.LogError("Cookies file path cannot be empty when CookieLogin is enabled.");
                return;
            }
        }

        // Avoid duplicates by login name
        settings.YouTubeSettings.YouTubeUsers.RemoveAll(u =>
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase));

        var userSettings = new YouTubeUserSettings
        {
            Login   = login,
            Id      = login,   // YouTube has no simple numeric ID; use the login as ID
            Enabled = true,
            CookieLogin = cookieLogin,
            CookiesFilePath = cookiesFilePath
        };

        settings.YouTubeSettings.YouTubeUsers.Add(userSettings);
        manager.Save(settings);

        logger.LogInformation("YouTube account '{Login}' added.", login);

        if (!cookieLogin)
        {
            logger.LogInformation(
                "On the first run the bot will open a browser window for you to log in to Google. " +
                "Your session will be saved so you only need to do this once.");
        }
        else
        {
            logger.LogInformation(
                "CookieLogin enabled. The bot will use your cookies file for YouTube authentication.");
        }
    }
}
