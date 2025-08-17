using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.TwitchGQL;
using Action = System.Action;

namespace TwitchDropsBot.WinForms
{
    public partial class TwitchUserTab : UserControl
    {
        private readonly TwitchUser twitchUser;
        public TabPage TabPage => currentTabPage;
        private readonly object imageListLock = new object();


        public TwitchUserTab(TwitchUser twitchUser)
        {
            InitializeComponent();

            try
            {
                InitializeListView();
            }
            catch (Exception ex)
            {
                twitchUser.Logger.Error(ex);
            }

            this.twitchUser = twitchUser;
            twitchUser.PropertyChanged += TwitchUser_PropertyChanged;

            currentTabPage.Text = twitchUser.Login;

            twitchUser.Logger.OnLog += (message) => AppendLog($"LOG: {message}");
            twitchUser.Logger.OnError += (message) => AppendLog($"ERROR: {message}");
            twitchUser.Logger.OnException += (exception) => AppendLog($"ERROR: {exception.ToString()}");
            twitchUser.Logger.OnInfo += (message) => AppendLog($"INFO: {message}");
        }

        private async void TwitchUser_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TwitchUser.Status))
            {
                await UpdateUI(twitchUser.Status);
            }

            if (e.PropertyName == nameof(TwitchUser.CurrentDropCurrentSession))
            {
                UpdateProgress();
            }

            if (e.PropertyName == nameof(TwitchUser.Inventory))
            {
                if (twitchUser?.Inventory != null)
                {
                    twitchUser.Logger.Info("Inventory requested");
                    try
                    {
                        await LoadInventoryAsync();
                    }
                    catch (Exception ex)
                    {
                        twitchUser.Logger.Error(ex);
                    }

                }
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

                    break;
                default:
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

        private async Task<ListViewGroup> AddGroup(IInventorySystem item)
        {
            var ifExist = inventoryListView.Groups.Cast<ListViewGroup>().FirstOrDefault(group => group.Header == item.GetGroup());

            if (ifExist != null)
            {
                return ifExist;
            }

            if (item.GetGameImageUrl(16) != null)
            {
                await DownloadImageFromWeb(item, gameImageList, item.GetGameSlug());
            }

            ListViewGroup group = new ListViewGroup(item.GetGroup(), HorizontalAlignment.Left);
            group.TitleImageKey = item.GetGameSlug();
            group.CollapsedState = ListViewGroupCollapsedState.Collapsed;
            if (inventoryListView.InvokeRequired)
            {
                inventoryListView.Invoke(new Action(() =>
                {
                    inventoryListView.Groups.Add(group);
                }));
            }
            else
            {
                inventoryListView.Groups.Add(group);
            }

            return group;
        }

        private async Task LoadInventoryAsync()
        {
            List<IInventorySystem> inventoryItems = new List<IInventorySystem>();
            var gameEventDrops = twitchUser.Inventory?.GameEventDrops?.OrderBy(drop => drop.lastAwardedAt).Reverse().ToList() ?? new List<GameEventDrop>();
            var dropCampaignsInProgress = twitchUser.Inventory?.DropCampaignsInProgress;

            if (dropCampaignsInProgress != null)
            {
                var timeBasedDrops = await Task.Run(() =>
                {
                    var itemList = new ConcurrentBag<TimeBasedDrop>();

                    Parallel.ForEach(dropCampaignsInProgress, dropCampaign =>
                    {
                        foreach (var timeBasedDrop in dropCampaign.TimeBasedDrops)
                        {
                            timeBasedDrop.Game = dropCampaign.Game;
                            itemList.Add(timeBasedDrop);
                        }
                    });

                    return itemList.ToList();
                });

                inventoryItems.AddRange(timeBasedDrops);
            }

            inventoryItems.AddRange(gameEventDrops);

            var oneItemOfEachDistinctSlug = inventoryItems
                .GroupBy(item => item.GetGameSlug())
                .Select(group => group.First())
                .ToList();

            // Create and add groups first
            var groupTasks = oneItemOfEachDistinctSlug.Select(async inventoryItem =>
            {
                return await AddGroup(inventoryItem);
            });

            var groups = await Task.WhenAll(groupTasks);

            // Create and add items
            var itemTasks = inventoryItems.Select(async inventoryItem =>
            {
                // Download the item's image
                await DownloadImageFromWeb(inventoryItem, dropsInventoryImageList);

                // Attribute the correct group to the item
                var group = groups.First(g => g.Header == inventoryItem.GetGroup());

                ListViewItem listViewItem = new ListViewItem()
                {
                    Group = group,
                    ImageKey = inventoryItem.Id
                };
                listViewItem.SubItems.Add(inventoryItem.GetName());
                listViewItem.SubItems.Add(inventoryItem.GetStatus());

                return listViewItem;
            });

            var listViewItemsResult = await Task.WhenAll(itemTasks);

            if (inventoryListView.InvokeRequired)
            {
                inventoryListView.Invoke(new Action(() =>
                {
                    inventoryListView.BeginUpdate();
                    inventoryListView.Items.Clear();
                    inventoryListView.Items.AddRange(listViewItemsResult.ToArray());
                    inventoryListView.AutoResizeColumn(2, ColumnHeaderAutoResizeStyle.ColumnContent);
                    inventoryListView.EndUpdate();
                }));
            }
            else
            {
                inventoryListView.BeginUpdate();
                inventoryListView.Items.Clear();
                inventoryListView.Items.AddRange(listViewItemsResult.ToArray());
                inventoryListView.AutoResizeColumn(2, ColumnHeaderAutoResizeStyle.ColumnContent);
                inventoryListView.EndUpdate();
            }
        }

        private async Task DownloadImageFromWeb(IInventorySystem item, ImageList il, string? slug = null)
        {
            var key = slug ?? item.Id;

            // Check if the image already exists in the ImageList
            lock (imageListLock)
            {
                if (il.Images.ContainsKey(key))
                {
                    return;
                }
            }

            await Task.Run(async () =>
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(slug != null ? item.GetGameImageUrl(16): item.GetImage());
                    response.EnsureSuccessStatusCode();
                    using (var respStream = await response.Content.ReadAsStreamAsync())
                    {
                        Bitmap bmp = new Bitmap(respStream);
                        if (inventoryListView.InvokeRequired)
                        {
                            inventoryListView.Invoke(new Action(() =>
                            {
                                lock (imageListLock)
                                {
                                    il.Images.Add(key, bmp);
                                }
                            }));
                        }
                        else
                        {
                            lock (imageListLock)
                            {
                                il.Images.Add(key, bmp);
                            }
                        }
                    }
                }
            });
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