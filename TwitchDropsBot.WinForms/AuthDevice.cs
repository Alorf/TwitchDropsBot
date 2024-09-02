using System.Diagnostics;
using System.Text.Json;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Utilities;

namespace TwitchDropsBot.WinForms
{
    public partial class AuthDevice : Form
    {
        private string? code;
        private CancellationTokenSource? cts;

        public AuthDevice()
        {
            InitializeComponent();
            this.Load += new EventHandler(AuthDevice_Load); // Subscribe to the Load event
            this.Disposed += new EventHandler(AuthDevice_Disposed); // Subscribe to the Disposed event

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
                Console.WriteLine("Operation Cancelled");
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
                var jsonResponse = await AuthSystem.GetCodeAsync();
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
                jsonResponse = await AuthSystem.CodeConfirmationAsync(deviceCode, token);
                CheckCancellation();

                if (jsonResponse == null)
                {
                    MessageBox.Show("Failed to authenticate the user.", "Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                var secret = jsonResponse.RootElement.GetProperty("access_token").GetString();

                CheckCancellation();
                ConfigUser user = await AuthSystem.ClientSecretUserAsync(secret);
                CheckCancellation();

                // Save the user into config.json
                var config = AppConfig.GetConfig();
                config.Users.RemoveAll(x => x.Id == user.Id);
                config.Users.Add(user);

                AppConfig.SaveConfig(config);

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
                SystemLogger.Info(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
