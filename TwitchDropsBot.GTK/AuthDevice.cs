using Gtk;
using System;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object;
using UI = Gtk.Builder.ObjectAttribute;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Exception;
using System.Linq;
using Microsoft.Win32;
using System.Reflection;
using TwitchDropsBot.Core.Object.TwitchGQL;
using TwitchDropsBot.Core.Utilities;

namespace TwitchDropsBot.GTK
{
    internal class AuthDevice : Dialog
    {

        [UI] private LinkButton linkButton;
        [UI] private Label codeLabel;
        [UI] private Label statusLabel;

        private string? code;
        private CancellationTokenSource? cts;

        public AuthDevice() : this(new Builder("MainWindow.glade"))
        {
        }

        private AuthDevice(Builder builder) : base(builder.GetRawOwnedObject("AuthDeviceDialog"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;
            Close += AuthDevice_Disposed;

            Shown += authDevice_Shown;
        }

        private void AuthDevice_Disposed(object sender, EventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        private async void authDevice_Shown(object sender, EventArgs e)
        {
            cts = new CancellationTokenSource();
            try
            {
                Task.Run(async () => { await AuthenticateDeviceAsync(cts.Token); });

            }
            catch (OperationCanceledException)
            {
                SystemLogger.Info("Operation Cancelled");
            }
        }

        private async Task AuthenticateDeviceAsync(CancellationToken token)
        {
            System.Action CheckCancellation() => () =>
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
                Application.Invoke(delegate
                {
                    codeLabel.Text = $"Please enter the code: {code}";
                    linkButton.Uri = verificationUri;
                    statusLabel.Text = $"Waiting for user to authenticate...";
                });

                CheckCancellation();
                jsonResponse = await AuthSystem.CodeConfirmationAsync(deviceCode, token);
                CheckCancellation();

                if (jsonResponse == null)
                {
                    ShowMessageDialog("Failed to authenticate the user.", "Error", MessageType.Error);
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

                Application.Invoke(delegate
                {
                    ShowMessageDialog("User authenticated and configuration saved.", "Success", MessageType.Info);
                    this.Respond(Gtk.ResponseType.Ok);
                    //close
                    this.Destroy();
                });
            }
            catch (OperationCanceledException ex)
            {
                SystemLogger.Info(ex.Message);
            }
            catch (Exception ex)
            {
                ShowMessageDialog($"An error occurred: {ex.Message}", "Error", MessageType.Error);
            }
        }

        private void ShowMessageDialog(string message, string title, MessageType messageType)
        {
            using (var dialog = new MessageDialog(this, DialogFlags.Modal, messageType, ButtonsType.Ok, message))
            {
                dialog.Title = title;
                dialog.Run();
                dialog.Destroy();
            }
        }
    }
}