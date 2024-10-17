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

        private TabPage CreateTabbPage(TwitchUser twitchUser)
        {
            var tabPage = new TabPage
            {
                Text = twitchUser.Login
            };

            tabPage.BackColor = Color.White;

            var twitchUserLogger = new TextBox
            {
                Location = new Point(6, 6),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(578, 328)
            };

            var labelGame = new Label
            {
                BackColor = System.Drawing.Color.Transparent,
                Location = new Point(6, 349),
                Size = new Size(284, 35),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Game : N/A",
            };

            var labelDrop = new Label
            {
                BackColor = System.Drawing.Color.Transparent,
                Location = new Point(6, 395),
                Size = new Size(284, 35),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"Drop : N/A"
            };

            var labelPercentage = new Label
            {
                BackColor = System.Drawing.Color.Transparent,
                Location = new Point(6, 453),
                Size = new Size(578, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "-%"
            };

            var labelMinRemaining = new Label()
            {
                BackColor = System.Drawing.Color.Transparent,
                Location = new Point(334, 453),
                Size = new Size(250, 19),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Minutes remaining : -"
            };

            var progressBar = new ProgressBar()
            {
                Location = new Point(6, 475),
                Size = new Size(578, 23),
                Value = 0
            };

            var reloadButton = new Button
            {
                Location = new Point(296, 349),
                Size = new Size(284, 35),
                Text = "Reload",
                UseVisualStyleBackColor = true
            };

            reloadButton.Click += (sender, e) =>
            {
                if (twitchUser.CancellationTokenSource != null &&
                    !twitchUser.CancellationTokenSource.IsCancellationRequested)
                {
                    twitchUser.Logger.Info("Reload requested");
                    twitchUser.CancellationTokenSource?.Cancel();
                }
            };

            tabPage.Controls.Add(twitchUserLogger);
            tabPage.Controls.Add(labelGame);
            tabPage.Controls.Add(labelDrop);
            tabPage.Controls.Add(labelMinRemaining);
            tabPage.Controls.Add(labelPercentage);
            tabPage.Controls.Add(progressBar);
            tabPage.Controls.Add(reloadButton);

            // Timer to update the controls periodically
            var timer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };

            timer.Tick += (sender, e) =>
            {
                switch (twitchUser.Status)
                {
                    case BotStatus.Idle:
                    case BotStatus.Seeking:
                        // reset every label
                        labelGame.Text = $"Game : N/A";
                        labelDrop.Text = $"Drop : N/A";
                        labelPercentage.Text = "-%";
                        labelMinRemaining.Text = "Minutes remaining : -";
                        progressBar.Value = 0;
                        break;
                    default:
                        labelGame.Text = $"Game : {twitchUser.CurrentCampaign?.Game.DisplayName}";
                        labelDrop.Text = $"Drop : {twitchUser.CurrentTimeBasedDrop?.Name}";

                        if (twitchUser.CurrentDropCurrentSession != null &&
                            twitchUser.CurrentDropCurrentSession.requiredMinutesWatched > 0)
                        {
                            var percentage = (int)((twitchUser.CurrentDropCurrentSession.CurrentMinutesWatched /
                                                    (double)twitchUser.CurrentDropCurrentSession
                                                        .requiredMinutesWatched) * 100);

                            if (percentage > 100) // for some reason it gave me 101 sometimes
                            {
                                percentage = 100;
                            }

                            progressBar.Value = percentage;
                            labelPercentage.Text = $"{percentage}%";
                            labelMinRemaining.Text =
                                $"Minutes remaining : {twitchUser.CurrentDropCurrentSession.requiredMinutesWatched - twitchUser.CurrentDropCurrentSession.CurrentMinutesWatched}";
                        }

                        break;
                }
            };

            timer.Start();

            twitchUser.Logger.OnLog += (message) => AppendLog($"LOG: {message}");
            twitchUser.Logger.OnError += (message) => AppendLog($"ERROR: {message}");
            twitchUser.Logger.OnInfo += (message) => AppendLog($"INFO: {message}");

            void AppendLog(string message)
            {
                if (twitchUserLogger.InvokeRequired)
                {
                    twitchUserLogger.Invoke(new Action(() =>
                    {
                        twitchUserLogger.AppendText($"[{DateTime.Now}] {message}{Environment.NewLine}");
                    }));
                }
                else
                {
                    twitchUserLogger.AppendText($"[{DateTime.Now}] {message}{Environment.NewLine}");
                }
            }

            return tabPage;
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