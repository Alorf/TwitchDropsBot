using TwitchDropsBot.Core;
using System.Runtime.InteropServices;
using System.Security.Policy;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Factories.User;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Settings;

namespace TwitchDropsBot.WinForms
{
    public partial class MainForm : Form
    {
        private IOptionsMonitor<BotSettings> _botSettings;
        private ILogger<MainForm> _logger;
        private UserFactory _userFactory;
        private SettingsManager _settingsManager;
        public MainForm(IOptionsMonitor<BotSettings> botSettings, ILogger<MainForm> logger, UserFactory userFactory, SettingsManager settingsManager)
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();
            
            _botSettings = botSettings;
            _logger = logger;
            _userFactory = userFactory;
            _settingsManager = settingsManager;

            checkBoxFavourite.Checked = _botSettings.CurrentValue.TwitchSettings.OnlyFavouriteGames;
            checkBoxStartup.Checked = _botSettings.CurrentValue.LaunchOnStartup;
            checkBoxMinimizeInTray.Checked = _botSettings.CurrentValue.MinimizeInTray;
            checkBoxConnectedAccounts.Checked = _botSettings.CurrentValue.TwitchSettings.OnlyConnectedAccounts;

            while (_botSettings.CurrentValue.TwitchSettings.TwitchUsers.Count == 0)
            {
                _logger.LogInformation("No users found in the _botSettingsuration file.");
                _logger.LogInformation("Login process will start.");

                AuthDevice authDevice = new AuthDevice(_botSettings, _logger, _settingsManager);
                authDevice.ShowDialog();

                if (authDevice.DialogResult == DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }
            }

            foreach (TwitchUserSettings userSettings in _botSettings.CurrentValue.TwitchSettings.TwitchUsers)
            {
                if (!userSettings.Enabled)
                {
                    _logger.LogInformation($"User {userSettings.Login} is not enabled, skipping...");
                    continue;
                } 
                
                var twitchUser = _userFactory.CreateTwitchUser(userSettings, true);
                twitchUser.StartBot();

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

            foreach (var game in _botSettings.CurrentValue.FavouriteGames)
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
            var new_botSettings = _settingsManager.Read();
            
            new_botSettings.LaunchOnStartup = checkBoxStartup.Checked;
            SetStartup(new_botSettings.LaunchOnStartup);
            
            _settingsManager.Save(new_botSettings);
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
            var new_botSettings = _settingsManager.Read();

            new_botSettings.TwitchSettings.OnlyFavouriteGames = checkBoxFavourite.Checked;

            _settingsManager.Save(new_botSettings);
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

            var new_botSettings = _settingsManager.Read();

            if (!_botSettings.CurrentValue.FavouriteGames.Contains(gameName))
            {
                new_botSettings.FavouriteGames.Add(gameName);
            }

           _settingsManager.Save(new_botSettings);

            FavGameListBox.Items.Add(gameName);
            FavGameListBox.SelectedItem = gameName;
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {

            if (FavGameListBox.SelectedItem != null)
            {
                string gameName = FavGameListBox.SelectedItem.ToString();

                var new_botSettings = _settingsManager.Read();

                if (_botSettings.CurrentValue.FavouriteGames.Contains(gameName))
                {
                    new_botSettings.FavouriteGames.Remove(gameName);
                }

                _settingsManager.Save(new_botSettings);

                FavGameListBox.Items.Remove(gameName);
            }
        }

        private void buttonUp_Click(object sender, EventArgs e)
        {

            if (FavGameListBox.SelectedItem != null && FavGameListBox.SelectedIndex > 0)
            {
                int index = FavGameListBox.SelectedIndex;
                string item = FavGameListBox.SelectedItem.ToString();
                var new_botSettings = _settingsManager.Read();

                new_botSettings.FavouriteGames.RemoveAt(index);
                new_botSettings.FavouriteGames.Insert(index - 1, item);

                _settingsManager.Save(new_botSettings);

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
                
                var new_botSettings = _settingsManager.Read();

                new_botSettings.FavouriteGames.RemoveAt(index);
                new_botSettings.FavouriteGames.Insert(index + 1, item);

                _settingsManager.Save(new_botSettings);

                FavGameListBox.Items.RemoveAt(index);
                FavGameListBox.Items.Insert(index + 1, item);
                FavGameListBox.SelectedIndex = index + 1;
            }
        }

        private void buttonAddNewAccount_Click(object sender, EventArgs e)
        {
            // Open auth device popup
            AuthDevice authDevice = new AuthDevice(_botSettings, _logger, _settingsManager);
            authDevice.ShowDialog();

            if (authDevice.DialogResult == DialogResult.Cancel || authDevice.DialogResult == DialogResult.Abort)
            {
                authDevice.Dispose();
                return;
            }

            // Create a bot for the new user
            var userSettings = _botSettings.CurrentValue.TwitchSettings.TwitchUsers.Last();
            var twitchUser = _userFactory.CreateTwitchUser(userSettings, true);
            twitchUser.StartBot();
            
            tabControl1.TabPages.Add(CreateTabPage(twitchUser));
        }

        private void buttonPutInTray_Click(object sender, EventArgs e)
        {
            putInTray();
        }

        private void CheckBoxMinimizeInTrayCheckedChanged(object sender, EventArgs e)
        {
            var new_botSettings = _settingsManager.Read();
            new_botSettings.MinimizeInTray = checkBoxMinimizeInTray.Checked;
            _settingsManager.Save(new_botSettings);
        }

        private void checkBoxConnectedAccounts_CheckedChanged(object sender, EventArgs e)
        {
            var new_botSettings = _settingsManager.Read();
            _botSettings.CurrentValue.TwitchSettings.OnlyConnectedAccounts = checkBoxConnectedAccounts.Checked;
            _settingsManager.Save(new_botSettings);

        }
    }
}