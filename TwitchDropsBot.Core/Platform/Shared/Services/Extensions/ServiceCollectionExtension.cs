using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using TwitchDropsBot.Core.Platform.Kick.Factories.Repositories;
using TwitchDropsBot.Core.Platform.Kick.Factories.WatchManager;
using TwitchDropsBot.Core.Platform.Kick.WatchManager;
using TwitchDropsBot.Core.Platform.Shared.Factories.Bot;
using TwitchDropsBot.Core.Platform.Shared.Factories.User;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Factories.WatchManager;
using TwitchDropsBot.Core.Platform.Twitch.Repository.Factory;

namespace TwitchDropsBot.Core.Platform.Shared.Services.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddBotService(this IServiceCollection services)
    {
        services.AddSingleton<NotificationService>();
        services.AddSingleton<BrowserService>();
        services.AddSingleton<UserFactory>();
        services.AddSingleton<BotFactory>();
        
        return services;
    }
    
    public static IServiceCollection AddTwitchService(this IServiceCollection services)
    {
        services.AddSingleton<ITwitchWatchManagerFactory, TwitchWatchManagerFactory>();
        services.AddSingleton<ITwitchRepositoryFactory, TwitchRepositoryFactory>();

        return services;
    }
    
    public static IServiceCollection AddKickService(this IServiceCollection services)
    {
        services.AddSingleton<IKickWatchManagerFactory, KickWatchManagerFactory>();
        services.AddSingleton<IKickRepositoryFactory, KickRepositoryFactory>();

        return services;
    }
}