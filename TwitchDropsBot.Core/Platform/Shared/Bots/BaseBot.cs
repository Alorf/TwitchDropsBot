using Discord;
using Serilog;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Shared.Bots;

public abstract class BaseBot<TUser> where TUser : BotUser
{
    public TUser BotUser;
    public BotSettings BotSettings;
    protected ILogger Logger;

    public BaseBot(TUser user, ILogger logger)
    {
        BotUser = user;
        BotSettings = AppSettingsService.Settings;

        Logger = logger;
    }

    public async Task StartBot()
    {
        TimeSpan waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);

        while(true){
            try
            {
                await StartAsync();
                waitingTime = TimeSpan.FromSeconds(20);
            }
            catch (NoBroadcasterOrNoCampaignLeft ex)
            {
                Logger.Debug(ex.Message);
                Logger.Debug($"Waiting {BotSettings.waitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
            }
            catch (StreamOffline ex)
            {
                Logger.Debug(ex.Message);
                Logger.Debug($"Waiting {BotSettings.waitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
            }
            catch (CurrentDropSessionChanged ex)
            {
                Logger.Debug(ex.Message);
                Logger.Debug($"Waiting {BotSettings.waitingSeconds} seconds before trying again.");
                waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
            }
            catch (OperationCanceledException ex)
            {
                Logger.Debug(ex.Message);
                waitingTime = TimeSpan.FromSeconds(10);
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex, ex.Message);

                if (!string.IsNullOrEmpty(AppSettingsService.Settings.WebhookURL))
                {
                    await BotUser.SendWebhookAsync(new List<Embed>
                    {
                        new EmbedBuilder()
                            .WithTitle($"ERROR : {BotUser.Login} - {DateTime.Now}")
                            .WithDescription($"```\n{ex}\n```")
                            .WithColor(Discord.Color.Red)
                            //.WithUrl(action.Url)
                            .Build()
                    });
                }

                waitingTime = TimeSpan.FromSeconds(BotSettings.waitingSeconds);
            }

            BotUser.Close();
            await Task.Delay(waitingTime);
        }
    }

    public abstract Task StartAsync();
}