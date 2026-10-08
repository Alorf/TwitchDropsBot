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
    public IOptionsMonitor<BotSettings> BotSettings;
    protected ILogger Logger;
    protected readonly NotificationService NotificationService;

    public BaseBot(TUser user, ILogger logger, NotificationService notificationService, IOptionsMonitor<BotSettings> botSettings)
    {
        BotUser = user;
        BotSettings = botSettings;
        Logger = logger;
        NotificationService = notificationService;
    }

    public abstract List<String> GetUserFavoriteGames();
    
    protected Task Notify(string title, string message, string? image = null)
        => NotificationService.SendNotification(BotUser, title, message, image != null ? new Uri(image) : null!);

    protected Task NotifyError(string title, string message, string? image = null)
        => NotificationService.SendErrorNotification(BotUser, title, message, image);

    public async Task StartBot()
    {
        TimeSpan waitingTime = TimeSpan.FromSeconds(BotSettings.CurrentValue.WaitingSeconds);
        
        while(true)
        {
            // A fresh token each cycle, so a re-pick request can end the current watch or wait
            var cycleCancellation = new CancellationTokenSource();
            BotUser.CancellationTokenSource = cycleCancellation;
            BotUser.NextCycleAt = null;

            try
            {
                await StartAsync();
                waitingTime = TimeSpan.FromSeconds(20);
                BotUser.IdleReason = BotIdleReason.CycleFinished;
            }
            catch (NoBroadcasterOrNoCampaignLeft ex)
            {
                Logger.LogDebug(ex.Message);
                Logger.LogDebug($"Waiting {BotSettings.CurrentValue.WaitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.CurrentValue.WaitingSeconds);
                BotUser.IdleReason = BotIdleReason.NothingToWatch;
            }
            catch (StreamOffline ex)
            {
                Logger.LogDebug(ex.Message);
                Logger.LogDebug($"Waiting {BotSettings.CurrentValue.WaitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.CurrentValue.WaitingSeconds);
                BotUser.IdleReason = BotIdleReason.StreamOffline;
            }
            catch (CurrentDropSessionChanged ex)
            {
                Logger.LogDebug(ex.Message);
                Logger.LogDebug($"Waiting {BotSettings.CurrentValue.WaitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.CurrentValue.WaitingSeconds);
                BotUser.IdleReason = BotIdleReason.DropSessionChanged;
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogDebug(ex.Message);
                waitingTime = TimeSpan.FromSeconds(10);
                BotUser.IdleReason = cycleCancellation.IsCancellationRequested
                    ? BotIdleReason.Switching
                    : BotIdleReason.Error;
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                BotUser.IdleReason = BotIdleReason.Error;
                BotUser.LastError = ex.Message;
                BotUser.LastErrorAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(BotSettings.CurrentValue.WebhookURL))
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
        
                waitingTime = TimeSpan.FromSeconds(BotSettings.CurrentValue.WaitingSeconds);
            }
        
            BotUser.Close();
            BotUser.NextCycleAt = DateTime.UtcNow + waitingTime;

            // A re-pick request during the wait starts the next cycle at once
            try
            {
                await Task.Delay(waitingTime,
                    cycleCancellation.IsCancellationRequested ? CancellationToken.None : cycleCancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    protected abstract Task StartAsync();
}