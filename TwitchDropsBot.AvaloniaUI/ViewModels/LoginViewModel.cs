using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Utilities;

namespace TwitchDropsBot.AvaloniaUI.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private AppConfig config;
    private string? code;
    public ICommand LoginCommand { get; }

    public LoginViewModel(){
    config = AppConfig.Instance;
    LoginCommand = ReactiveCommand.CreateFromTask(onClick, outputScheduler: RxApp.TaskpoolScheduler);
}

private async Task onClick()
{
        try
        {
            var jsonResponse = await AuthSystem.GetCodeAsync();
            var deviceCode = jsonResponse.RootElement.GetProperty("device_code").GetString();
            var userCode = jsonResponse.RootElement.GetProperty("user_code").GetString();
            var verificationUri = jsonResponse.RootElement.GetProperty("verification_uri").GetString();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var url = $"https://www.twitch.tv/activate?device-code={userCode}";
                var sInfo = new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                };
                Process.Start(sInfo);
            });

            var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2)); // Timeout de 2 minutes
            var token = tokenSource.Token;
            var authResult = await Task.Run(async () =>
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var pollResponse = await AuthSystem.CodeConfirmationAsync(deviceCode, token);
                    if (pollResponse != null)
                        return pollResponse;
                    await Task.Delay(2000, token);
                }
            }, token);

            var secret = authResult.RootElement.GetProperty("access_token").GetString();
            ConfigUser user = await AuthSystem.ClientSecretUserAsync(secret);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                config.Users.RemoveAll(x => x.Id == user.Id);
                config.Users.Add(user);
                config.SaveConfig();
            });
        }
        catch (OperationCanceledException ex)
        {
            SystemLogger.Info("Auth cancelled: " + ex.Message);
        }
        catch (Exception ex)
        {
            SystemLogger.Info("Auth error: " + ex.Message);
        }
    }
}