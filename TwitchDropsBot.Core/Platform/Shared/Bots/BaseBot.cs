using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Shared.Bots;

public abstract class BaseBot<TUser> where TUser : BotUser
{
    public TUser BotUser;
    public BotSettings BotSettings;
    protected ILogger Logger;
    protected readonly NotificationService NotificationService;

    public BaseBot(TUser user, ILogger logger, NotificationService notificationService, IOptionsMonitor<BotSettings> botSettings)
    {
        BotUser = user;
        BotSettings = botSettings.CurrentValue;
        Logger = logger;
        NotificationService = notificationService;
    }
    
    protected Task Notify(string title, string message, string? image = null)
        => NotificationService.SendNotification(BotUser, title, message, image != null ? new Uri(image) : null!);

    protected Task NotifyError(string title, string message, string? image = null)
        => NotificationService.SendErrorNotification(BotUser, title, message, image);

    public async Task StartBot()
    {
        TimeSpan waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
        
        while(true)
        {
            try
            {
                await StartAsync();
                waitingTime = TimeSpan.FromSeconds(20);
            }
            catch (NoBroadcasterOrNoCampaignLeft ex)
            {
                Logger.LogDebug(ex.Message);
                Logger.LogDebug($"Waiting {BotSettings.waitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
            }
            catch (StreamOffline ex)
            {
                Logger.LogDebug(ex.Message);
                Logger.LogDebug($"Waiting {BotSettings.waitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
            }
            catch (CurrentDropSessionChanged ex)
            {
                Logger.LogDebug(ex.Message);
                Logger.LogDebug($"Waiting {BotSettings.waitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogDebug(ex.Message);
                waitingTime = TimeSpan.FromSeconds(10);
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, ex.Message);
        
                if (!string.IsNullOrEmpty(BotSettings.WebhookURL))
                {
                    await BotUser.SendWebhookAsync(new List<Embed>
                    {
                        new EmbedBuilder()
                            .WithTitle($"ERROR : {BotUser.Login} - {DateTime.Now}")
                            .WithDescription($"```\n{ex}\n```")
                            .WithColor(Discord.Color.Red)
                            .Build()
                    });
                }
        
                waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
            }
        
            BotUser.Close();
            await Task.Delay(waitingTime);
        }
    }

    protected abstract Task StartAsync();
}