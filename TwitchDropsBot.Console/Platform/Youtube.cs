using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Youtube.Settings;

namespace TwitchDropsBot.Console.Platform;

public static class Youtube
{
    private const int TimeoutSeconds = 100;

    public static async Task AuthYoutubeDeviceAsync(ILogger logger, SettingsManager manager)
    {
        logger.LogInformation("Enter a display name / login for this YouTube account:");
        var login = System.Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(login))
        {
            logger.LogError("Login cannot be empty.");
            return;
        }

        var cookieFilePath = Path.Combine(AppContext.BaseDirectory, $"cookie-{login}.txt");
        logger.LogInformation("Cookie file path: {Path}", cookieFilePath);

        if (!File.Exists(cookieFilePath))
        {
            File.WriteAllText(cookieFilePath, "");
            logger.LogInformation("Created cookie file at: {Path}", cookieFilePath);
            logger.LogInformation("Please paste your cookies (Netscape format) into the file within {Timeout} seconds...", TimeoutSeconds);

            await Task.Delay(TimeSpan.FromSeconds(5));

            var deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));

                var content = File.ReadAllText(cookieFilePath);
                logger.LogInformation("File length: {Length}", content.Length);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    logger.LogInformation("Cookie file has content, continuing...");
                    break;
                }
            }

            if (File.ReadAllText(cookieFilePath).Length == 0)
            {
                logger.LogWarning("No cookies added within {Timeout} seconds. Proceeding anyway.", TimeoutSeconds);
            }
        }

        var settings = manager.Read();

        settings.YoutubeSettings.YoutubeUsers.RemoveAll(u =>
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase));

        var userSettings = new YoutubeUserSettings
        {
            Login = login,
            Id = login,
            Enabled = true
        };

        settings.YoutubeSettings.YoutubeUsers.Add(userSettings);
        manager.Save(settings);

        logger.LogInformation("YouTube account '{Login}' added.", login);
    }
}