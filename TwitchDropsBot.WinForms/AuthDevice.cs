using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core;

using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Services;

namespace TwitchDropsBot.WinForms
{
    public partial class AuthDevice : Form
    {
        private string? code;
        private CancellationTokenSource? cts;
        private IOptionsMonitor<BotSettings> config;
        private ILogger _logger;
        private SettingsManager _settingsManager;

        public AuthDevice(IOptionsMonitor<BotSettings> botSettings, ILogger logger, SettingsManager settingsManager)
        {
            config = botSettings;
            _logger = logger;
            _settingsManager = settingsManager;
            this.Load += new EventHandler(AuthDevice_Load);
            this.Disposed += new EventHandler(AuthDevice_Disposed);
            InitializeComponent();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo($"https://www.twitch.tv/activate?device-code={code}")
            {
                UseShellExecute = true
            };

            if (!string.IsNullOrEmpty(code))
            {
                Process.Start(sInfo);
            }
        }

        private async void AuthDevice_Load(object sender, EventArgs e)
        {
            cts = new CancellationTokenSource();
            try
            {
                Task.Run(async () =>
                {
                    await AuthenticateDeviceAsync(cts.Token);
                });

            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operation Cancelled");
            }
        }

        private void AuthDevice_Disposed(object sender, EventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private async Task AuthenticateDeviceAsync(CancellationToken token)
        {
            Action CheckCancellation() => () =>
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
            };
            try
            {
                CheckCancellation();
                var jsonResponse = await TwitchAuthService.GetCodeAsync();
                var deviceCode = jsonResponse.RootElement.GetProperty("device_code").GetString();
                code = jsonResponse.RootElement.GetProperty("user_code").GetString();
                var verificationUri = jsonResponse.RootElement.GetProperty("verification_uri").GetString();
                CheckCancellation();

                // Update UI with verification URI and user code
                this.Invoke((MethodInvoker)delegate
                {
                    AuthCode.Text = $"Please enter the code: {code}";
                    AuthCode.TextAlign = ContentAlignment.MiddleCenter;
                    AuthStatus.Text = $"Waiting for user to authenticate...";
                });

                CheckCancellation();
                jsonResponse = await TwitchAuthService.CodeConfirmationAsync(deviceCode, _logger, token);
                CheckCancellation();

                if (jsonResponse == null)
                {
                    MessageBox.Show("Failed to authenticate the user.", "Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                var secret = jsonResponse.RootElement.GetProperty("access_token").GetString();

                CheckCancellation();
                var user = await TwitchAuthService.ClientSecretUserAsync(secret);
                CheckCancellation();

                // Save the user into config.CurrentValue.json
                var newConfig = _settingsManager.Read();
                newConfig.TwitchSettings.TwitchUsers.RemoveAll(x => x.Id == user.Id);
                newConfig.TwitchSettings.TwitchUsers.Add(user);
                
                _settingsManager.Save(newConfig);

                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show("User authenticated and configuration saved.", "Success", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                });
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
