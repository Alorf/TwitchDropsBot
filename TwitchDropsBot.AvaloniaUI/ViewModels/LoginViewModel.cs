using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive;
using CommunityToolkit.Mvvm.Input;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Utilities;

namespace TwitchDropsBot.AvaloniaUI.ViewModels
{
    public partial class LoginViewModel : ReactiveObject
    {
        private readonly AppConfig _config;
        private readonly BotViewModel _botPage;

        private bool _isAuthenticationStarted;
        private string _statusMessage;
        private string _userCode;

        private ReactiveCommand<Unit, Unit> _startAuthenticationCommand;
        private CancellationTokenSource _authCancellationTokenSource;
        private int _authRunning = 0;
        
        
        public LoginViewModel(BotViewModel botPage)
        {
            _botPage = botPage;
            _config = AppConfig.Instance;
            
            StatusMessage = "Ready for authentication";
        }

        public bool IsAuthenticationStarted
        {
            get => _isAuthenticationStarted;
            set => this.RaiseAndSetIfChanged(ref _isAuthenticationStarted, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public string UserCode
        {
            get => _userCode;
            set => this.RaiseAndSetIfChanged(ref _userCode, value);
        }
        
        [RelayCommand]
        private async Task StartAuthenticationAsync()
        {
            // Avoid multiple authentication attempts
            if (Interlocked.Exchange(ref _authRunning, 1) == 1)
                return;

            CancelAuthentication();

            try
            {
                _authCancellationTokenSource = new CancellationTokenSource();
                var token = _authCancellationTokenSource.Token;

                IsAuthenticationStarted = true;
                StatusMessage = "Gaining authentification code...";

                var jsonResponse = await AuthSystem.GetCodeAsync();
                var deviceCode = jsonResponse.RootElement.GetProperty("device_code").GetString();
                var userCode = jsonResponse.RootElement.GetProperty("user_code").GetString();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UserCode = userCode;
                    StatusMessage = $"Please enter the code : {userCode}";
                }, DispatcherPriority.Normal);

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

                await Dispatcher.UIThread.InvokeAsync(() =>
                    StatusMessage = "Gaining access token...");

                var secret = authResult.RootElement.GetProperty("access_token").GetString();
                ConfigUser user = await AuthSystem.ClientSecretUserAsync(secret);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _config.Users.RemoveAll(x => x.Id == user.Id);
                    _config.Users.Add(user);
                    _config.SaveConfig();
                    StatusMessage = $"User {user.Login} fully authentified!";
                    _botPage.OnUserAuthenticated(user);
                });
            }
            catch (OperationCanceledException ex)
            {
                SystemLogger.Info($"Authentification cancelled by user.: {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() =>
                    StatusMessage = "Authentification cancelled by user.");
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    StatusMessage = $"Erreur: {ex.Message}");
                SystemLogger.Error($"Authentification error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _authRunning, 0);

                await Dispatcher.UIThread.InvokeAsync(() => IsAuthenticationStarted = false);
                _authCancellationTokenSource?.Dispose();
                _authCancellationTokenSource = null;
            }
        }

        public void CancelAuthentication()
        {
            if (_authCancellationTokenSource != null && !_authCancellationTokenSource.IsCancellationRequested)
            {
                SystemLogger.Info("Authentification cancelled by user.");
                _authCancellationTokenSource.Cancel();
            }
        }

        public void ResetState()
        {
            CancelAuthentication();

            IsAuthenticationStarted = false;
            StatusMessage = "Ready for authentication";
            UserCode = string.Empty;
        }

        [RelayCommand]
        private async Task OpenBrowserAsync()
        {
            if (!string.IsNullOrEmpty(UserCode))
            {
                try
                {
                    var url = "https://www.twitch.tv/activate?device-code=" + UserCode;
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    SystemLogger.Info($"Error while oppening the browser: {ex.Message}");
                }
            }
        }
    }
}