namespace TwitchDropsBot.Core.Platform.Shared.Helpers;

public static class ConfigPathHelper
{
    public static string GetConfigFilePath(string fileName)
    {
        var insideDockerEnv = Environment.GetEnvironmentVariable("INSIDE_DOCKER");
        var isInsideDocker = !string.IsNullOrEmpty(insideDockerEnv) && insideDockerEnv.ToLower() == "true";

        string directory = isInsideDocker
            ? Path.Combine(AppContext.BaseDirectory, "Configuration")
            : AppContext.BaseDirectory;

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        var path = Path.Combine(directory, fileName);
        
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "{}");
        }

        return path;
    }
}