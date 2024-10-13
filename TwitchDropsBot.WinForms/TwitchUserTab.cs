using System.Reflection;
using TwitchDropsBot.Core.Object;

namespace TwitchDropsBot.WinForms
{
    public partial class TwitchUserTab : UserControl
    {
        private readonly TwitchUser twitchUser;
        private Boolean flag = false;


        public TabPage TabPage => currentTabPage;

        public TwitchUserTab(TwitchUser twitchUser)
        {
            InitializeComponent();
            this.twitchUser = twitchUser;
            currentTabPage.Text = twitchUser.Login;
            flag = true;

            Start();

            //Inventory table IMG | TEXT
            //Populate inventory table
        }

        public void Start()
        {
            // Timer to update the controls periodically
            var timer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };

            timer.Tick += async (sender, e) =>
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

                        if (flag && twitchUser.Inventory != null)
                        {
                            twitchUser.Logger.Info("Inventory requested");
                            await LoadInventoryAsync();
                        }
                        break;
                    default:
                        labelGame.Text = $"Game : {twitchUser.CurrentCampaign?.Game.DisplayName}";
                        labelDrop.Text = $"Drop : {twitchUser.CurrentTimeBasedDrop?.Name}";

                        if (twitchUser.CurrendDropCurrentSession != null &&
                            twitchUser.CurrendDropCurrentSession.requiredMinutesWatched > 0)
                        {
                            var percentage = (int)((twitchUser.CurrendDropCurrentSession.CurrentMinutesWatched /
                                                    (double)twitchUser.CurrendDropCurrentSession
                                                        .requiredMinutesWatched) * 100);

                            if (percentage > 100) // for some reason it gave me 101 sometimes
                            {
                                percentage = 100;
                            }

                            progressBar.Value = percentage;
                            labelPercentage.Text = $"{percentage}%";
                            labelMinRemaining.Text =
                                $"Minutes remaining : {twitchUser.CurrendDropCurrentSession.requiredMinutesWatched - twitchUser.CurrendDropCurrentSession.CurrentMinutesWatched}";
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
                if (twitchLoggerTextBox.InvokeRequired)
                {
                    twitchLoggerTextBox.Invoke(new Action(() =>
                    {
                        twitchLoggerTextBox.AppendText($"[{DateTime.Now}] {message}{Environment.NewLine}");
                    }));
                }
                else
                {
                    twitchLoggerTextBox.AppendText($"[{DateTime.Now}] {message}{Environment.NewLine}");
                }
            }
        }

        private async Task LoadInventoryAsync()
        {
            flag = false;
            var inventoryItems = twitchUser.Inventory?.GameEventDrops;
            if (inventoryItems != null)
            {
                // Perform sorting and control creation in a background task
                var controlsToAdd = await Task.Run(() =>
                {
                    var controls = new List<Control>();

                    var orderedItems = inventoryItems.OrderBy(item => item.lastAwardedAt).Reverse().Take(75);

                    foreach (var item in orderedItems)
                    {
                        var inventoryRow = new InventoryRow(item);
                        controls.Add(inventoryRow);
                    }

                    return controls;
                });

                // Ensure UI updates are done on the main thread
                flowLayoutPanel1.Invoke(new Action(() =>
                {
                    flowLayoutPanel1.Controls.AddRange(controlsToAdd.ToArray());
                }));
            }
        }


        private void ReloadButton_Click(object sender, EventArgs e)
        {
            if (twitchUser.CancellationTokenSource != null &&
                !twitchUser.CancellationTokenSource.IsCancellationRequested)
            {
                twitchUser.Logger.Info("Reload requested");
                twitchUser.CancellationTokenSource?.Cancel();
            }
        }
    }
}
