using System.Text.Json;
using TwitchDropsBot.Core.Platform.Shared.Settings;

public class SettingsManager
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public SettingsManager(string filePath)
    {
        _filePath = filePath;

        var dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_filePath))
            Save(new BotSettings());
    }

    public BotSettings Read()
    {
        lock (_lock)
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<BotSettings>(json) ?? new BotSettings();
        }
    }

    public void Save(BotSettings settings)
    {
        lock (_lock)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, options));
        }
    }
}