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
        private AppConfig config;
        public MainForm()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();

            config = AppConfig.Instance;

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
            }

            foreach (ConfigUser user in config.Users)
            {
                TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId, user.FavouriteGames);
                twitchUser.DiscordWebhookURl = config.WebhookURL;

                if (!user.Enabled)
                {
                    SystemLogger.Info($"User {twitchUser.Login} is not enabled, skipping...");
                    continue;
                }                
                Bot.StartBot(twitchUser);
                tabControl1.TabPages.Add(CreateTabPage(twitchUser));

                InitList();
            }

#if DEBUG
            AllocConsole();
#endif
        }

        void InitList()
        {
            FavGameListBox.Items.Clear();

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
            config.LaunchOnStartup = checkBoxStartup.Checked;

            SetStartup(config.LaunchOnStartup);


            config.SaveConfig();
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
            config.OnlyFavouriteGames = checkBoxFavourite.Checked;

            config.SaveConfig();
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

            if (!config.FavouriteGames.Contains(gameName))
            {
                config.FavouriteGames.Add(gameName);
            }

            config.SaveConfig();

            FavGameListBox.Items.Add(gameName);
            FavGameListBox.SelectedItem = gameName;
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            if (FavGameListBox.SelectedItem != null)
            {
                string gameName = FavGameListBox.SelectedItem.ToString();

                if (config.FavouriteGames.Contains(gameName))
                {
                    config.FavouriteGames.Remove(gameName);
                }

                config.SaveConfig();

                FavGameListBox.Items.Remove(gameName);
            }
        }

        private void buttonUp_Click(object sender, EventArgs e)
        {
            if (FavGameListBox.SelectedItem != null && FavGameListBox.SelectedIndex > 0)
            {
                int index = FavGameListBox.SelectedIndex;
                string item = FavGameListBox.SelectedItem.ToString();

                config.FavouriteGames.RemoveAt(index);
                config.FavouriteGames.Insert(index - 1, item);

                config.SaveConfig();

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

                config.FavouriteGames.RemoveAt(index);
                config.FavouriteGames.Insert(index + 1, item);

                config.SaveConfig();

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
            ConfigUser user = config.Users.Last();
            TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId, user.FavouriteGames);
            twitchUser.DiscordWebhookURl = config.WebhookURL;

            Bot.StartBot(twitchUser);

            tabControl1.TabPages.Add(CreateTabPage(twitchUser));
        }

        private void buttonPutInTray_Click(object sender, EventArgs e)
        {
            putInTray();
        }

        private void CheckBoxMinimizeInTrayCheckedChanged(object sender, EventArgs e)
        {
            config.MinimizeInTray = checkBoxMinimizeInTray.Checked;

            config.SaveConfig();
        }

        private void checkBoxConnectedAccounts_CheckedChanged(object sender, EventArgs e)
        {
            config.OnlyConnectedAccounts = checkBoxConnectedAccounts.Checked;

            config.SaveConfig();
        }
    }
}