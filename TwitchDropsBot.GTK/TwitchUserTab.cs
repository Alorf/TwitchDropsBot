using Gtk;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.TwitchGQL;
using UI = Gtk.Builder.ObjectAttribute;

namespace TwitchDropsBot.GTK
{
    internal class TwitchUserTab : Window
    {
        private readonly TwitchUser twitchUser;

        [UI] private Notebook userNotebook = null;
        [UI] private Label gameLabel = null;
        [UI] private Button reloadButton = null;
        [UI] private Label dropLabel = null;
        [UI] private Label minutesRemainingLabel = null;
        [UI] private Label percentageLabel = null;
        [UI] private LevelBar levelBar = null;
        [UI] private TreeView inventoryTreeView = null;
        [UI] private ListStore inventoryListStore = null;

        public TwitchUserTab(TwitchUser twitchUser) : this(new Builder("TwitchUserTab.glade"))
        {
            this.twitchUser = twitchUser;
            twitchUser.PropertyChanged += TwitchUser_PropertyChanged;

        }

        private TwitchUserTab(Builder builder) : base(builder.GetRawOwnedObject("TwitchUserTab"))
        {
            builder.Autoconnect(this);


            DeleteEvent += Window_DeleteEvent;
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
                    await LoadInventoryAsync();
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

                levelBar.Value = percentage;
                percentageLabel.Text = $"{percentage}%";
                minutesRemainingLabel.Text =
                    $"Minutes remaining : {twitchUser.CurrentDropCurrentSession.requiredMinutesWatched - twitchUser.CurrentDropCurrentSession.CurrentMinutesWatched}";
            }
        }

        private async Task LoadInventoryAsync()
        {
            try
            {
                var items = new ListStore(typeof(string), typeof(string), typeof(string));
                var inventoryItems = twitchUser.Inventory?.GameEventDrops?.OrderBy(drop => drop.lastAwardedAt).Reverse().ToList() ?? new List<GameEventDrop>();
                var dropCampaignsInProgress = twitchUser.Inventory?.DropCampaignsInProgress;

                if (dropCampaignsInProgress != null)
                {
                    foreach (var dropCampaign in dropCampaignsInProgress)
                    {
                        var group = AddGroup(dropCampaign.Game.Name);

                        foreach (var timeBasedDrop in dropCampaign.TimeBasedDrops)
                        {
                            items.AppendValues(timeBasedDrop.Name, $"{timeBasedDrop.Self.CurrentMinutesWatched}/{timeBasedDrop.RequiredMinutesWatched} minutes watched", timeBasedDrop.Self.IsClaimed ? "\u2714" : "\u274C");
                        }
                    }
                }

                if (inventoryItems != null)
                {
                    foreach (var inventoryItem in inventoryItems)
                    {
                        items.AppendValues(inventoryItem.Name, "", inventoryItem.IsConnected ? "\u2714" : "\u26A0");
                    }
                }

                Application.Invoke(delegate
                {
                    try
                    {
                        inventoryListStore.Clear();
                        inventoryListStore = items;
                        inventoryTreeView.Model = inventoryListStore; // Ensure the TreeView model is updated
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating TreeView: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading inventory: {ex.Message}");
            }
        }

        private TreeViewColumn AddGroup(string groupName)
        {
            var column = new TreeViewColumn { Title = groupName };
            inventoryTreeView.AppendColumn(column);
            return column;
        }
    }
}
