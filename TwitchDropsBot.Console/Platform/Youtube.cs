using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Youtube.Utils;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Youtube.Settings;

namespace TwitchDropsBot.Console.Platform;

public static class Youtube
{
    private const string YoutubeLoginUrl = "https://accounts.google.com/ServiceLogin?service=youtube&continue=https://www.youtube.com/";

    public static Task AuthYoutubeDeviceAsync(ILogger logger, SettingsManager manager)
    {
        logger.LogInformation("Enter a display name / login for this YouTube account:");
        var login = System.Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(login))
        {
            logger.LogError("Login cannot be empty.");
            return Task.CompletedTask;
        }

        logger.LogInformation("Select YouTube authentication mode:");
        logger.LogInformation("1. Open browser and authenticate, then paste cookies");
        logger.LogInformation("2. Paste cookies directly");
        logger.LogInformation("3. Cancel");

        var mode = ReadMode();
        if (mode == "3")
        {
            logger.LogInformation("YouTube authentication cancelled.");
            return Task.CompletedTask;
        }

        if (mode == "1")
        {
            OpenYoutubeLoginPage(logger);
            logger.LogInformation("Authenticate in your browser, then press ENTER to continue.");
            System.Console.ReadLine();
        }

        var rawCookieInput = ReadCookieInput(logger);
        var normalizedCookies = YoutubeCookieParser.NormalizeForStorage(rawCookieInput);
        if (string.IsNullOrWhiteSpace(normalizedCookies))
        {
            if (mode == "2")
            {
                logger.LogError("No valid cookies detected. Account not added.");
                return Task.CompletedTask;
            }

            logger.LogWarning("No valid cookies detected. The account will be added without cookies.");
        }

        var settings = manager.Read();

        settings.YoutubeSettings.YoutubeUsers.RemoveAll(u =>
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase));

        var userSettings = new YoutubeUserSettings
        {
            Login = login,
            Id = login,
            Enabled = true,
            Cookies = normalizedCookies
        };

        settings.YoutubeSettings.YoutubeUsers.Add(userSettings);
        manager.Save(settings);

        logger.LogInformation("YouTube account '{Login}' added with inline cookie storage in config.json.", login);
        return Task.CompletedTask;
    }

    private static string ReadMode()
    {
        while (true)
        {
            var value = System.Console.ReadLine()?.Trim();
            if (value is null)
                return "3";

            if (value is "1" or "2" or "3")
                return value;

            System.Console.WriteLine("Please enter 1, 2, or 3:");
        }
    }

    private static string ReadCookieInput(ILogger logger)
    {
        logger.LogInformation("Paste your extracted cookies, then type END on a new line:");
        var lines = new List<string>();

        while (true)
        {
            var line = System.Console.ReadLine();

            if (line is null)
            {
                logger.LogWarning("Input stream closed while reading cookies.");
                break;
            }

            if (string.Equals(line.Trim(), "END", StringComparison.OrdinalIgnoreCase))
                break;

            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void OpenYoutubeLoginPage(ILogger logger)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = YoutubeLoginUrl,
                UseShellExecute = true
            });
            logger.LogInformation("Opened browser for YouTube authentication.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to automatically open browser. Open this URL manually: {Url}", YoutubeLoginUrl);
        }
    }
}
