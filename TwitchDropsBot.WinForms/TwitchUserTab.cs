using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.WinForms
{
    public partial class TwitchUserTab : UserControl
    {
        private readonly TwitchUser twitchUser;
        private Boolean flag;

        public TabPage TabPage => currentTabPage;

        public TwitchUserTab(TwitchUser twitchUser)
        {
            InitializeComponent();
            InitializeListView();

            this.twitchUser = twitchUser;
            twitchUser.PropertyChanged += TwitchUser_PropertyChanged;

            currentTabPage.Text = twitchUser.Login;
            flag = true;

            twitchUser.Logger.OnLog += (message) => AppendLog($"LOG: {message}");
            twitchUser.Logger.OnError += (message) => AppendLog($"ERROR: {message}");
            twitchUser.Logger.OnInfo += (message) => AppendLog($"INFO: {message}");
        }

        private async void TwitchUser_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine(e.PropertyName);

            if (e.PropertyName == nameof(TwitchUser.Status))
            {
                await UpdateUI(twitchUser.Status);
            }

            if (e.PropertyName == nameof(TwitchUser.CurrentDropCurrentSession))
            {
                UpdateProgress();
            }
        }

        public async Task UpdateUI(BotStatus status)
        {

            switch (twitchUser.Status)
            {
                case BotStatus.Idle:
                case BotStatus.Seeking:
                    // reset every label
                    labelGame.Invoke((System.Windows.Forms.MethodInvoker)(() => labelGame.Text = $"Game : N/A"));
                    labelDrop.Invoke((System.Windows.Forms.MethodInvoker)(() => labelDrop.Text = $"Drop : N/A"));
                    labelDrop.Invoke((System.Windows.Forms.MethodInvoker)(() => labelPercentage.Text = $"-%"));
                    labelMinRemaining.Invoke((System.Windows.Forms.MethodInvoker)(() => labelMinRemaining.Text = $"Minutes remaining : -"));
                    progressBar.Invoke((System.Windows.Forms.MethodInvoker)(() => progressBar.Value = 0));

                    if (flag && twitchUser.Inventory != null)
                    {
                        twitchUser.Logger.Info("Inventory requested");
                        await LoadInventoryAsync();
                    }
                    break;
                default:
                    flag = true;
                    labelGame.Invoke((System.Windows.Forms.MethodInvoker)(() => labelGame.Text = $"Game : {twitchUser.CurrentCampaign?.Game.DisplayName}"));
                    labelDrop.Invoke((System.Windows.Forms.MethodInvoker)(() => labelDrop.Text = $"Drop : {twitchUser.CurrentTimeBasedDrop?.Name}"));
                    break;
            }
        }

        private void AppendLog(string message)
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


        private void UpdateProgress()
        {
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

                progressBar.Invoke((System.Windows.Forms.MethodInvoker)(() => progressBar.Value = percentage));
                labelPercentage.Invoke((System.Windows.Forms.MethodInvoker)(() => labelPercentage.Text = $"{percentage}%"));
                labelMinRemaining.Invoke((System.Windows.Forms.MethodInvoker)(() => labelMinRemaining.Text =
                                           $"Minutes remaining : {twitchUser.CurrentDropCurrentSession.requiredMinutesWatched - twitchUser.CurrentDropCurrentSession.CurrentMinutesWatched}"));
            }
        }

        private void InitializeListView()
        {
            dropsInventoryImageList.ImageSize = new System.Drawing.Size(64, 64);
            gameImageList.ImageSize = new System.Drawing.Size(16, 16);
            inventoryListView.LargeImageList = dropsInventoryImageList;
            inventoryListView.SmallImageList = dropsInventoryImageList;
            inventoryListView.GroupImageList = gameImageList;

            // Add columns with initial width
            inventoryListView.Columns.Add("Name", (int)(inventoryListView.Width * 0.3)); // 30% of ListView width
            inventoryListView.Columns.Add("Status", (int)(inventoryListView.Width * 0.45)); // 50% of ListView width
            inventoryListView.Columns.Add("Test", (int)(inventoryListView.Width * 0.1)); // 20% of ListView width

            // Handle the resize event to adjust column widths dynamically
            inventoryListView.Resize += (sender, e) =>
            {
                int totalWidth = inventoryListView.Width;
                inventoryListView.Columns[0].Width = (int)(totalWidth * 0.3);
                inventoryListView.Columns[1].Width = (int)(totalWidth * 0.45);
                inventoryListView.Columns[2].Width = (int)(totalWidth * 0.1);
            };
        }

        private ListViewGroup AddGroup(string groupName, string? slug = null)
        {
            var ifExist = inventoryListView.Groups.Cast<ListViewGroup>().FirstOrDefault(group => group.Header == groupName); ;

            if (ifExist != null)
            {
                return ifExist;
            }

            ListViewGroup group = new ListViewGroup(groupName, HorizontalAlignment.Left);
            group.CollapsedState = ListViewGroupCollapsedState.Expanded;
            if (inventoryListView.InvokeRequired)
            {
                inventoryListView.Invoke(new Action(() =>
                {
                    group.TitleImageKey = slug;
                    inventoryListView.Groups.Add(group);
                }));
            }
            else
            {
                inventoryListView.Groups.Add(group);
                group.TitleImageKey = groupName;
            }

            return group;
        }

        private async Task LoadInventoryAsync()
        {
            flag = false;
            List<ListViewItem> items = new List<ListViewItem>();
            var inventoryItems = twitchUser.Inventory?.GameEventDrops?.OrderBy(drop => drop.lastAwardedAt).Reverse().ToList() ?? new List<GameEventDrop>();
            var dropCampaignsInProgress = twitchUser.Inventory?.DropCampaignsInProgress;

            if (dropCampaignsInProgress != null)
            {
                var timeBasedDrops = await Task.Run(async () =>
                {
                    List<ListViewItem> itemList = new List<ListViewItem>();
                    foreach (var dropCampaign in dropCampaignsInProgress)
                    {
                        var url = dropCampaign.Game.BoxArtURL;
                        //replace width and height
                        url = url.Replace("{width}", "64");
                        url = url.Replace("{height}", "64");

                        await DownloadImageFromWeb(url, dropCampaign.Game.Slug, gameImageList);

                        var group = AddGroup(dropCampaign.Game.Name, dropCampaign.Game.Slug);

                        foreach (var timeBasedDrop in dropCampaign.TimeBasedDrops)
                        {
                            // Download images from the web
                            await DownloadImageFromWeb(timeBasedDrop.BenefitEdges[0].Benefit.ImageAssetURL, timeBasedDrop.Id, dropsInventoryImageList);

                            ListViewItem lst = new ListViewItem
                            {
                                ImageKey = timeBasedDrop.Id,
                                Group = group
                            };
                            lst.SubItems.Add($"{timeBasedDrop.Name}\n{timeBasedDrop.Self.CurrentMinutesWatched}/{timeBasedDrop.RequiredMinutesWatched} minutes watched");
                            lst.SubItems.Add(timeBasedDrop.Self.IsClaimed ? "\u2714" : "\u26A0");
                            lst.ToolTipText = timeBasedDrop.Self.IsClaimed ? "Claimed" : "Account not connected";

                            itemList.Add(lst);
                        }
                    }
                    return itemList;
                });

                items.AddRange(timeBasedDrops);
            }

            if (inventoryItems != null)
            {
                var gameEventDrops = await Task.Run(async () =>
                {
                    var group = AddGroup("Inventory");

                    List<ListViewItem> itemList = new List<ListViewItem>();
                    foreach (var inventoryItem in inventoryItems)
                    {
                        // Download images from the web
                        await DownloadImageFromWeb(inventoryItem.ImageURL, inventoryItem.Id, dropsInventoryImageList);

                        ListViewItem lst = new ListViewItem
                        {
                            ImageKey = inventoryItem.Id,
                            Group = group
                        };
                        lst.SubItems.Add($"{inventoryItem.Name}");
                        lst.SubItems.Add(inventoryItem.IsConnected ? "\u2714" : "\u26A0");
                        lst.ToolTipText = inventoryItem.IsConnected ? "Claimed" : "Account not connected";

                        itemList.Add(lst);
                    }
                    return itemList;
                });

                items.AddRange(gameEventDrops);
            }

            if (items.Count != 0)
            {
                if (inventoryListView.InvokeRequired)
                {
                    inventoryListView.Invoke(new Action(() =>
                    {
                        inventoryListView.Items.Clear();
                        inventoryListView.Items.AddRange(items.ToArray());
                        inventoryListView.AutoResizeColumn(2, ColumnHeaderAutoResizeStyle.ColumnContent);
                    }));
                }
                else
                {
                    inventoryListView.Items.Clear();
                    inventoryListView.Items.AddRange(items.ToArray());
                    inventoryListView.AutoResizeColumn(2, ColumnHeaderAutoResizeStyle.ColumnContent);
                }
            }

        }

        private async Task DownloadImageFromWeb(string imageUrl, string key, ImageList il)
        {
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(imageUrl);
                response.EnsureSuccessStatusCode();
                using (var respStream = await response.Content.ReadAsStreamAsync())
                {
                    Bitmap bmp = new Bitmap(respStream);
                    if (inventoryListView.InvokeRequired)
                    {
                        inventoryListView.Invoke(new Action(() => il.Images.Add(key, bmp)));
                    }
                    else
                    {
                        il.Images.Add(key, bmp);
                    }
                }
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
