using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core;
using System.Runtime.InteropServices;
using System.Security.Policy;
using TwitchDropsBot.Core.Exception;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using TwitchDropsBot.Core.Object.Config;

namespace TwitchDropsBot.WinForms
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();

            AppConfig config = AppConfig.GetConfig();

            checkBoxFavourite.Checked = config.OnlyFavouriteGames;
            checkBoxStartup.Checked = config.LaunchOnStartup;
            checkBoxMinimizeInTray.Checked = config.MinimizeInTray;
            checkBoxConnectedAccounts.Checked = config.OnlyConnectedAccounts;

            while (config.Users.Count == 0)
            {
                SystemLogger.Info("No users found in the configuration file.");
                SystemLogger.Info("Login process will start.");

                AuthDevice authDevice = new AuthDevice();
                authDevice.ShowDialog();

                if (authDevice.DialogResult == DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }

                config = AppConfig.GetConfig();
            }

            foreach (ConfigUser user in config.Users)
            {
                TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId);
                twitchUser.DiscordWebhookURl = config.WebhookURL;

                StartBot(twitchUser);
                tabControl1.TabPages.Add(CreateTabPage(twitchUser));

                InitList();
            }

#if DEBUG
            AllocConsole();
#endif
        }

        private Task StartBot(TwitchUser twitchUser)
        {
            Bot bot = new Bot(twitchUser);
            TimeSpan waitingTime;
            twitchUser.CancellationTokenSource = new CancellationTokenSource();
            return Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await bot.StartAsync();
                        waitingTime = TimeSpan.FromSeconds(20);
                    }
                    catch (NoBroadcasterOrNoCampaignLeft ex)
                    {
                        twitchUser.Logger.Info(ex.Message);
                        twitchUser.Logger.Info("Waiting 5 minutes before trying again.");
                        waitingTime = TimeSpan.FromMinutes(5);
                    }
                    catch (StreamOffline ex)
                    {
                        twitchUser.Logger.Info(ex.Message);
                        twitchUser.Logger.Info("Waiting 5 minutes before trying again.");
                        waitingTime = TimeSpan.FromMinutes(5);
                    }
                    catch (OperationCanceledException ex)
                    {
                        twitchUser.Logger.Info(ex.Message);
                        twitchUser.CancellationTokenSource = new CancellationTokenSource();
                        waitingTime = TimeSpan.FromSeconds(10);
                    }
                    catch (Exception ex)
                    {
                        twitchUser.Logger.Error(ex);
                        waitingTime = TimeSpan.FromMinutes(5);
                    }

                    twitchUser.StreamURL = null;
                    twitchUser.Status = BotStatus.Idle;

                    await Task.Delay(waitingTime);
                }
            });
        }

        void InitList()
        {
            FavGameListBox.Items.Clear();
            AppConfig config = AppConfig.GetConfig();

            foreach (var game in config.FavouriteGames)
            {
                FavGameListBox.Items.Add(game);
            }
        }

#if DEBUG

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
#endif

        private void Form1_Load(object sender, EventArgs e)
        {
            notifyIcon1.BalloonTipTitle = "TwitchDropsBot";
            notifyIcon1.BalloonTipText = "The application has been put in the tray";
            notifyIcon1.Text = "TwitchDropsBot";
            notifyIcon1.BalloonTipClicked += notifyIcon1_BalloonTipClicked;
        }

        //balloon tip click
        private void notifyIcon1_BalloonTipClicked(object sender, EventArgs e)
        {
            this.Show();
            notifyIcon1.Visible = false;
            WindowState = FormWindowState.Normal;
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            //if mouseclick is left
            if (e.Button == MouseButtons.Left)
            {
                this.Show();
                notifyIcon1.Visible = false;
                WindowState = FormWindowState.Normal;
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && checkBoxMinimizeInTray.Checked)
            {
                putInTray();
            }
            else if (FormWindowState.Normal == this.WindowState)
            {
                notifyIcon1.Visible = false;
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void putInTray()
        {
            this.Hide();
            notifyIcon1.Visible = true;
            notifyIcon1.ShowBalloonTip(1000);
        }

        private TabPage CreateTabPage(TwitchUser twitchUser)
        {
            var userTab = new TwitchUserTab(twitchUser);

            return userTab.TabPage;
        }

        private void checkBoxStartup_CheckedChanged(object sender, EventArgs e)
        {
            //change appConfig
            AppConfig config = AppConfig.GetConfig();

            config.LaunchOnStartup = checkBoxStartup.Checked;

            SetStartup(config.LaunchOnStartup);


            AppConfig.SaveConfig(config);
        }

        private void SetStartup(bool enable)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (enable)
            {
                rk.SetValue("TwitchDropsBot", Application.ExecutablePath.ToString());
            }
            else
            {
                rk.DeleteValue("TwitchDropsBot", false);
            }
        }

        private void checkBoxFavourite_CheckedChanged(object sender, EventArgs e)
        {
            //change appConfig
            AppConfig config = AppConfig.GetConfig();

            config.OnlyFavouriteGames = checkBoxFavourite.Checked;

            AppConfig.SaveConfig(config);
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            string gameName = textBoxNameOfGame.Text;

            if (string.IsNullOrEmpty(gameName) || string.IsNullOrWhiteSpace(gameName) || FavGameListBox.Items.Contains(gameName))
            {
                if (FavGameListBox.Items.Contains(gameName))
                {
                    FavGameListBox.SelectedItem = gameName;
                }
                return;
            }

            AppConfig config = AppConfig.GetConfig();

            if (!config.FavouriteGames.Contains(gameName))
            {
                config.FavouriteGames.Add(gameName);
            }

            AppConfig.SaveConfig(config);

            FavGameListBox.Items.Add(gameName);
            FavGameListBox.SelectedItem = gameName;
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            if (FavGameListBox.SelectedItem != null)
            {
                string gameName = FavGameListBox.SelectedItem.ToString();

                AppConfig config = AppConfig.GetConfig();

                if (config.FavouriteGames.Contains(gameName))
                {
                    config.FavouriteGames.Remove(gameName);
                }

                AppConfig.SaveConfig(config);

                FavGameListBox.Items.Remove(gameName);
            }
        }

        private void buttonUp_Click(object sender, EventArgs e)
        {
            if (FavGameListBox.SelectedItem != null && FavGameListBox.SelectedIndex > 0)
            {
                int index = FavGameListBox.SelectedIndex;
                string item = FavGameListBox.SelectedItem.ToString();

                AppConfig config = AppConfig.GetConfig();

                config.FavouriteGames.RemoveAt(index);
                config.FavouriteGames.Insert(index - 1, item);

                AppConfig.SaveConfig(config);

                FavGameListBox.Items.RemoveAt(index);
                FavGameListBox.Items.Insert(index - 1, item);
                FavGameListBox.SelectedIndex = index - 1;
            }
        }

        private void buttonDown_Click(object sender, EventArgs e)
        {
            if (FavGameListBox.SelectedItem != null && FavGameListBox.SelectedIndex < FavGameListBox.Items.Count - 1)
            {
                int index = FavGameListBox.SelectedIndex;
                string item = FavGameListBox.SelectedItem.ToString();

                AppConfig config = AppConfig.GetConfig();

                config.FavouriteGames.RemoveAt(index);
                config.FavouriteGames.Insert(index + 1, item);

                AppConfig.SaveConfig(config);

                FavGameListBox.Items.RemoveAt(index);
                FavGameListBox.Items.Insert(index + 1, item);
                FavGameListBox.SelectedIndex = index + 1;
            }
        }

        private void buttonAddNewAccount_Click(object sender, EventArgs e)
        {
            // Open auth device popup
            AuthDevice authDevice = new AuthDevice();
            authDevice.ShowDialog();

            if (authDevice.DialogResult == DialogResult.Cancel || authDevice.DialogResult == DialogResult.Abort)
            {
                authDevice.Dispose();
                return;
            }

            // Create a bot for the new user
            AppConfig config = AppConfig.GetConfig();
            ConfigUser user = config.Users.Last();
            TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId);
            twitchUser.DiscordWebhookURl = config.WebhookURL;

            StartBot(twitchUser);

            tabControl1.TabPages.Add(CreateTabPage(twitchUser));
        }

        private void buttonPutInTray_Click(object sender, EventArgs e)
        {
            putInTray();
        }

        private void CheckBoxMinimizeInTrayCheckedChanged(object sender, EventArgs e)
        {
            //change appConfig
            AppConfig config = AppConfig.GetConfig();

            config.MinimizeInTray = checkBoxMinimizeInTray.Checked;

            AppConfig.SaveConfig(config);
        }

        private void checkBoxConnectedAccounts_CheckedChanged(object sender, EventArgs e)
        {
            //change appConfig
            AppConfig config = AppConfig.GetConfig();

            config.OnlyConnectedAccounts = checkBoxConnectedAccounts.Checked;

            AppConfig.SaveConfig(config);
        }
    }
}