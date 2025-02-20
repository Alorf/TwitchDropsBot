using Gdk;
using Gtk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.TwitchGQL;
using UI = Gtk.Builder.ObjectAttribute;

namespace TwitchDropsBot.GTK
{
    internal class TwitchUserTab : Gtk.Window
    {
        private readonly TwitchUser twitchUser;
        private readonly object logLock = new object(); // Lock object for synchronization

        [UI] private Notebook userNotebook = null;
        [UI] private Label gameLabel = null;
        [UI] private Label dropLabel = null;
        [UI] private Label minutesRemainingLabel = null;
        [UI] private Label percentageLabel = null;
        [UI] private Button reloadButton = null;
        [UI] private TextView twitchLoggerTextView = null;
        [UI] private LevelBar levelBar = null;
        [UI] private TreeView inventoryTreeView = null;

        public TwitchUserTab(TwitchUser twitchUser) : this(new Builder("TwitchUserTab.glade"))
        {
            this.twitchUser = twitchUser;
            twitchUser.PropertyChanged += TwitchUser_PropertyChanged;
            reloadButton.Clicked += ReloadButton_Click;
            
            twitchUser.Logger.OnLog += (message) => AppendLog($"LOG: {message}");
            twitchUser.Logger.OnError += (message) => AppendLog($"ERROR: {message}");
            twitchUser.Logger.OnException += (exception) => AppendLog($"ERROR: {exception.ToString()}");
            twitchUser.Logger.OnInfo += (message) => AppendLog($"INFO: {message}");
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

        private Task AppendLog(string message)
        {
            Application.Invoke(delegate
            {
                Threads.AddIdle(0, () =>
                {
                    var buffer = twitchLoggerTextView.Buffer;
                    buffer.Text += $"[{DateTime.Now}] {message}{Environment.NewLine}";
                    twitchLoggerTextView.ScrollToIter(buffer.EndIter, 0, false, 0, 0);
                    return false;
                });
            });
            return Task.CompletedTask;
        }

        private TwitchUserTab(Builder builder) : base(builder.GetRawOwnedObject("TwitchUserTab"))
        {
            builder.Autoconnect(this);

            this.DeleteEvent += Window_DeleteEvent;
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        public Widget GetFirstPage()
        {
            var userTabContent = userNotebook.GetNthPage(0);

            if (userTabContent != null)
            {
                userNotebook.RemovePage(0);
                return userTabContent;
            }

            return null;
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
            await Task.Run(() =>
            {
                Threads.AddIdle(0, () =>
                {
                    switch (twitchUser.Status)
                    {
                        case BotStatus.Idle:
                        case BotStatus.Seeking:
                            // reset every label
                            gameLabel.Text = "Game : N/A";
                            dropLabel.Text = "Drop : N/A";
                            percentageLabel.Text = "-%";
                            minutesRemainingLabel.Text = "Minutes remaining : -";
                            levelBar.Value = 0;
                            break;
                        default:
                            gameLabel.Text = $"Game : {twitchUser.CurrentCampaign?.Game.DisplayName}";
                            dropLabel.Text = $"Drop : {twitchUser.CurrentTimeBasedDrop?.Name}";
                            break;
                    }

                    return false;
                });
            });
        }

        private void UpdateProgress()
        {
            Threads.AddIdle(0, () =>
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

                    levelBar.Value = percentage;
                    percentageLabel.Text = $"{percentage}%";
                    minutesRemainingLabel.Text =
                        $"Minutes remaining : {twitchUser.CurrentDropCurrentSession.requiredMinutesWatched - twitchUser.CurrentDropCurrentSession.CurrentMinutesWatched}";
                }

                return false;
            });
            
        }

        private void InitializeInventoryTreeView(TreeStore inventoryTreeStore)
        {
            inventoryTreeView.Model = inventoryTreeStore;

            // Ensure TreeView columns are set up
            if (inventoryTreeView.Columns.Length == 0)
            {
                var imageColumn = new TreeViewColumn();
                var imageCell = new CellRendererPixbuf();
                imageColumn.PackStart(imageCell, true);
                imageColumn.AddAttribute(imageCell, "pixbuf", 0);

                var nameColumn = new TreeViewColumn();
                var nameCell = new CellRendererText();
                nameColumn.PackStart(nameCell, true);
                nameColumn.AddAttribute(nameCell, "text", 1);

                var descriptionColumn = new TreeViewColumn();
                var descriptionCell = new CellRendererText();
                descriptionColumn.PackStart(descriptionCell, false);
                descriptionColumn.AddAttribute(descriptionCell, "text", 2);

                inventoryTreeView.AppendColumn(imageColumn);
                inventoryTreeView.AppendColumn(nameColumn);
                inventoryTreeView.AppendColumn(descriptionColumn);
                inventoryTreeView.SizeAllocated += (o, args) => SetColumnWidths();
            }

            // Hide column headers
            inventoryTreeView.HeadersVisible = false;
        }

        void SetColumnWidths()
        {
            int totalWidth = inventoryTreeView.Allocation.Width;

            // Calculate widths based on percentages
            int imageColumnWidth = (int)(totalWidth * 0.45); // 20%
            int nameColumnWidth = (int)(totalWidth * 0.45); // 40%
            int descriptionColumnWidth = (int)(totalWidth * 0.10); // 40%

            var nameCell = inventoryTreeView.Columns[1].Cells[0] as CellRendererText;
            nameCell.WrapMode = Pango.WrapMode.Word;
            nameCell.WrapWidth = descriptionColumnWidth;

            // Set the widths
            inventoryTreeView.Columns[0].FixedWidth = imageColumnWidth;
            inventoryTreeView.Columns[1].FixedWidth = nameColumnWidth;
            inventoryTreeView.Columns[2].FixedWidth = descriptionColumnWidth;
        }

        private async Task LoadInventoryAsync()
        {
            var inventoryTreeStore = new TreeStore(typeof(Pixbuf), typeof(string), typeof(string));
            Threads.AddIdle(0, () =>
            {
                InitializeInventoryTreeView(inventoryTreeStore);
                return false;
            });

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

            Application.Invoke(delegate
            {
                Dictionary<string, TreeIter> gameGroups = new Dictionary<string, TreeIter>();
                List<Task<(Pixbuf, IInventorySystem)>> downloadTasks = new List<Task<(Pixbuf, IInventorySystem)>>();

                foreach (var item in inventoryItems)
                {
                    downloadTasks.Add(Task.Run(async () =>
                    {
                        var imagePixbuf = await DownloadImageFromWeb(item.GetImage());
                        imagePixbuf = imagePixbuf.ScaleSimple(64, 64, InterpType.Bilinear);
                        return (imagePixbuf, item);
                    }));
                }

                Task.WhenAll(downloadTasks).ContinueWith(t =>
                {
                    if (t.IsCanceled)
                    {
                        twitchUser.Logger.Info("LoadInventoryAsync was canceled.");
                        return;
                    }

                    if (t.IsFaulted)
                    {
                        twitchUser.Logger.Error("An error occurred while loading inventory.");
                        return;
                    }

                    Application.Invoke(delegate
                    {
                        foreach (var task in t.Result)
                        {
                            var (imagePixbuf, item) = task;
                            var gameName = item.GetGroup();
                            if (!gameGroups.ContainsKey(gameName))
                            {
                                // Create a new group for the game
                                var gameIter = inventoryTreeStore.AppendValues(null, gameName, null);
                                gameGroups[gameName] = gameIter;
                            }

                            inventoryTreeStore.AppendValues(gameGroups[gameName], imagePixbuf, item.GetName(), item.GetStatus());
                        }
                        inventoryTreeView.ShowAll();
                    });
                });
            });
        }

        private async Task<Pixbuf> DownloadImageFromWeb(string imageUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(imageUrl);
                response.EnsureSuccessStatusCode();
                using (var respStream = await response.Content.ReadAsStreamAsync())
                {
                    return new Pixbuf(respStream);
                }
            }
        }
    }
}
