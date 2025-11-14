using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using ILogger = Serilog.ILogger;

namespace TwitchDropsBot.Core.Platform.Shared.Services;

public static class AppService
{
    private static ServiceProvider? _serviceProvider;
    private static UISink _uiSink;

    public static ServiceCollection Init()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile("appsettings.Development.json", true, true);

        var config = builder.Build();
        var services = new ServiceCollection();

        _uiSink = new UISink();

        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .WriteTo.Sink(_uiSink)
            .CreateLogger();

        services.AddLogging(b => b.AddSerilog(logger));
        services.AddSingleton<ILogger>(logger);
        services.AddSingleton<IConfiguration>(config);

        _serviceProvider = services.BuildServiceProvider();

        return services;
    }

    public static ILogger GetLogger()
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("AppService has not been initialized.");

        return _serviceProvider.GetRequiredService<ILogger>();
    }
    
    public static IConfiguration GetConfiguration()
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("AppService has not been initialized.");

        return _serviceProvider.GetRequiredService<IConfiguration>();
    }
    
    public static UISink GetUISink()
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("AppService has not been initialized.");
        
        return _uiSink;
    }
}