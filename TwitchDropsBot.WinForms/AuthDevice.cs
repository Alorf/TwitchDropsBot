using System.Diagnostics;
using System.Text.Json;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Utilities;

namespace TwitchDropsBot.WinForms
{
    public partial class AuthDevice : Form
    {
        private string? code;

        public AuthDevice()
        {
            InitializeComponent();
            this.Load += new EventHandler(AuthDevice_Load); // Subscribe to the Load event
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
            await AuthenticateDeviceAsync();
        }

        private async Task AuthenticateDeviceAsync()
        {
            try
            {
                var jsonResponse = await AuthSystem.GetCodeAsync();
                var deviceCode = jsonResponse.RootElement.GetProperty("device_code").GetString();
                code = jsonResponse.RootElement.GetProperty("user_code").GetString();
                var verificationUri = jsonResponse.RootElement.GetProperty("verification_uri").GetString();

                // Update UI with verification URI and user code
                AuthCode.Text = $"Please enter the code: {code}";
                AuthCode.TextAlign = ContentAlignment.MiddleCenter;
                AuthStatus.Text = $"Waiting for user to authenticate...";


                jsonResponse = await AuthSystem.CodeConfirmationAsync(deviceCode);

                if (jsonResponse == null)
                {
                    MessageBox.Show("Failed to authenticate the user.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var secret = jsonResponse.RootElement.GetProperty("access_token").GetString();

                ConfigUser user = await AuthSystem.ClientSecretUserAsync(secret);

                // Save the user into config.json
                var config = AppConfig.GetConfig();
                config.Users.RemoveAll(x => x.Id == user.Id);
                config.Users.Add(user);

                AppConfig.SaveConfig(config);

                MessageBox.Show("User authenticated and configuration saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
