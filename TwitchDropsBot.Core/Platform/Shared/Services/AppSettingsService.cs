using System.Text.Encodings.Web;
using System.Text.Json;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

public class AppSettingsService
{
    private static BotSettings? _settings;
    private static readonly object _lock = new();
    private static FileSystemWatcher? _watcher;
    private static DateTime _lastRead = DateTime.MinValue;

    private static string ConfigPath
    {
        get
        {
            var insideDockerEnv = Environment.GetEnvironmentVariable("INSIDE_DOCKER");

            var isInsideDocker = insideDockerEnv != null && insideDockerEnv.ToLower() == "true";
            
            if (isInsideDocker)
            {
                var configDirectory = Path.Combine(AppContext.BaseDirectory, "Configuration");
                Directory.CreateDirectory(configDirectory);
                return configDirectory;    
            }
            
            return Path.Combine(AppContext.BaseDirectory);
        }
    }

    public static BotSettings Settings
    {
        get
        {
            if (_settings == null)
            {
                lock (_lock)
                {
                    if (_settings == null)
                    {
                        _settings = LoadConfig();
                        SetupWatcher();
                    }
                }
            }
            return _settings;
        }
    }

    private static void SetupWatcher()
    {
        var configFileName = "config.json";
        
        _watcher = new FileSystemWatcher(ConfigPath, configFileName);
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.Changed += OnConfigFileChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            if (DateTime.Now - _lastRead < TimeSpan.FromSeconds(1))
            {
                return;
            }
            _lastRead = DateTime.Now;
            
            try
            {
                Thread.Sleep(100); 
                _settings = LoadConfig();
                SystemLoggerService.Logger.Information("Configuration reloaded.");
            }
            catch (Exception ex)
            {
                SystemLoggerService.Logger.Error($"Error reloading configuration: {ex.Message}");
            }
        }
    }

    private static BotSettings LoadConfig()
    {
        var filePath = Path.Combine(ConfigPath, "config.json");

        if (!File.Exists(filePath))
        {
            return new BotSettings();
        }

        var jsonString = File.ReadAllText(filePath);

        return JsonSerializer.Deserialize<BotSettings>(jsonString) ?? new BotSettings();
    }

    public static void SaveConfig()
    {
        lock (_lock)
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var jsonString = JsonSerializer.Serialize(_settings, options);
                var filePath = Path.Combine(ConfigPath, "config.json");

                _lastRead = DateTime.Now;
                File.WriteAllText(filePath, jsonString);
            }
            finally
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = true;
                }
            }
        }
    }
}