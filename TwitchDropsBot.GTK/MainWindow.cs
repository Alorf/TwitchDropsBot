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

namespace TwitchDropsBot.GTK
{
    internal class MainWindow : Window
    {

        //Get GUI elements
        [UI] private Notebook usersNotebook = null;
        [UI] private ListBox favGameListBox = null;

        [UI] private CheckButton launchOnStartupCheckbox = null;
        [UI] private CheckButton onlyFavouritesCheckbox = null;
        [UI] private CheckButton onlyConnectedCheckbox = null;
        [UI] private CheckButton putInTrayCheckbox = null;

        [UI] private Button addGameButton = null;
        [UI] private Button upButton = null;
        [UI] private Button downButton = null;
        [UI] private Button deleteButton = null;
        [UI] private Button addAccountButton = null;
        [UI] private Button putInTrayButton = null;

        [UI] private Entry nameOfGameTextBox = null;

        public MainWindow() : this(new Builder("MainWindow.glade"))
        {
            AppConfig config = AppConfig.GetConfig();

            addAccountButton.Clicked += buttonAddNewAccount_Click;

            // Set the checkbox values
            launchOnStartupCheckbox.Active = config.LaunchOnStartup;
            onlyFavouritesCheckbox.Active = config.OnlyFavouriteGames;
            onlyConnectedCheckbox.Active = config.OnlyConnectedAccounts;
            putInTrayCheckbox.Active = config.MinimizeInTray;

            //if linux, disable launch on startup
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                launchOnStartupCheckbox.Sensitive = false;
            }

            while (config.Users.Count == 0)
            {
                SystemLogger.Info("No users found in the configuration file.");
                SystemLogger.Info("Login process will start.");

                AuthDevice authDevice = new AuthDevice();
                ResponseType response = (ResponseType)authDevice.Run();

                switch (response)
                {
                    case ResponseType.Cancel:
                        authDevice.Destroy();
                        Application.Quit();
                        break;
                }

                config = AppConfig.GetConfig();
            }

            foreach (ConfigUser user in config.Users)
            {
                TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId);

                StartBot(twitchUser);
                usersNotebook.AppendPage(CreateTabPage(twitchUser), new Label(twitchUser.Login));
                usersNotebook.ShowAll();

                InitList();
            }

            InitEvents();
            InitButtons();
        }

        private void buttonAddNewAccount_Click(object sender, EventArgs e)
        {
            AuthDevice authDevice = new AuthDevice();
            ResponseType response = (ResponseType)authDevice.Run();

            switch (response)
            {
                case ResponseType.Cancel:
                    authDevice.Destroy();
                    return;
                    break;
                default:
                    authDevice.Destroy();
                    break;
            }

            // Create a bot for the new user
            AppConfig config = AppConfig.GetConfig();
            ConfigUser user = config.Users.Last();
            TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId);

            StartBot(twitchUser);

            usersNotebook.AppendPage(CreateTabPage(twitchUser), new Label(twitchUser.Login));
            usersNotebook.ShowAll();
        }

        private void InitEvents()
        {
            launchOnStartupCheckbox.Toggled += (sender, args) =>
            {
                AppConfig config = AppConfig.GetConfig();
                config.LaunchOnStartup = launchOnStartupCheckbox.Active;
                SetStartup(launchOnStartupCheckbox.Active);
                AppConfig.SaveConfig(config);
            };

            onlyFavouritesCheckbox.Toggled += (sender, args) =>
            {
                AppConfig config = AppConfig.GetConfig();
                config.OnlyFavouriteGames = onlyFavouritesCheckbox.Active;
                AppConfig.SaveConfig(config);
            };

            onlyConnectedCheckbox.Toggled += (sender, args) =>
            {
                AppConfig config = AppConfig.GetConfig();
                config.OnlyConnectedAccounts = onlyConnectedCheckbox.Active;
                AppConfig.SaveConfig(config);
            };

            putInTrayCheckbox.Toggled += (sender, args) =>
            {
                AppConfig config = AppConfig.GetConfig();
                config.MinimizeInTray = putInTrayCheckbox.Active;
                AppConfig.SaveConfig(config);
            };

            //favgame lost focus
            favGameListBox.FocusOutEvent += (sender, args) =>
            {
                favGameListBox.UnselectAll();
            };
        }

        private void SetStartup(bool enable)
        {
            string executablePath = Assembly.GetExecutingAssembly().Location;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Use the registry to set startup
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (enable)
                {
                    rk.SetValue("TwitchDropsBot", executablePath);
                }
                else
                {
                    rk.DeleteValue("TwitchDropsBot", false);
                }
            }
        }
        private void InitButtons()
        {
            addGameButton.Clicked += (sender, args) =>
            {
                string gameName = nameOfGameTextBox.Text;

                if (string.IsNullOrEmpty(gameName) || string.IsNullOrWhiteSpace(gameName) || IsGameInList(gameName))
                {
                    SelectGameInList(gameName);
                    return;
                }

                AppConfig config = AppConfig.GetConfig();

                if (!config.FavouriteGames.Contains(gameName))
                {
                    config.FavouriteGames.Add(gameName);
                }

                AppConfig.SaveConfig(config);

                var newRow = new ListBoxRow();
                newRow.Add(new Label(gameName) { Halign = Align.Start });
                newRow.ShowAll();
                favGameListBox.Add(newRow);
                favGameListBox.ShowAll();
                SelectGameInList(gameName);
            };

            deleteButton.Clicked += (sender, args) =>
            {
                var selectedRow = favGameListBox.SelectedRow;
                if (selectedRow != null)
                {
                    string gameName = ((Label)selectedRow.Child).Text;

                    AppConfig config = AppConfig.GetConfig();

                    if (config.FavouriteGames.Contains(gameName))
                    {
                        config.FavouriteGames.Remove(gameName);
                    }

                    AppConfig.SaveConfig(config);

                    favGameListBox.Remove(selectedRow);
                }
            };

            upButton.Clicked += (sender, args) =>
            {
                var selectedRow = favGameListBox.SelectedRow;
                if (selectedRow != null)
                {
                    int index = GetRowIndex(selectedRow);
                    if (index > 0)
                    {
                        string gameName = ((Label)selectedRow.Child).Text;

                        AppConfig config = AppConfig.GetConfig();

                        config.FavouriteGames.RemoveAt(index);
                        config.FavouriteGames.Insert(index - 1, gameName);

                        AppConfig.SaveConfig(config);

                        MoveRow(selectedRow, index - 1);
                    }
                }
            };

            downButton.Clicked += (sender, args) =>
            {
                var selectedRow = favGameListBox.SelectedRow;
                if (selectedRow != null)
                {
                    int index = GetRowIndex(selectedRow);
                    if (index < favGameListBox.Children.Length - 1)
                    {
                        string gameName = ((Label)selectedRow.Child).Text;

                        AppConfig config = AppConfig.GetConfig();

                        config.FavouriteGames.RemoveAt(index);
                        config.FavouriteGames.Insert(index + 1, gameName);

                        AppConfig.SaveConfig(config);

                        MoveRow(selectedRow, index + 1);
                    }
                }
            };
        }

        private bool IsGameInList(string gameName)
        {
            foreach (ListBoxRow row in favGameListBox.Children)
            {
                if (((Label)row.Child).Text == gameName)
                {
                    return true;
                }
            }
            return false;
        }

        private void SelectGameInList(string gameName)
        {
            favGameListBox.UnselectAll();
            foreach (ListBoxRow row in favGameListBox.Children)
            {
                if (((Label)row.Child).Text == gameName)
                {
                    favGameListBox.SelectRow(row);
                    return;
                }
            }
        }

        private int GetRowIndex(ListBoxRow row)
        {
            int index = 0;
            foreach (var child in favGameListBox.Children)
            {
                if (child == row)
                {
                    return index;
                }
                index++;
            }
            return -1; // Row not found
        }

        private void MoveRow(ListBoxRow row, int newIndex)
        {
            favGameListBox.Remove(row);
            favGameListBox.Insert(row, newIndex);
            row.ShowAll();

            favGameListBox.UnselectAll();
            favGameListBox.SelectRow(row);
        }

        private void InitList()
        {
            // Clear existing items
            foreach (var child in favGameListBox.Children)
            {
                favGameListBox.Remove(child);
            }

            AppConfig config = AppConfig.GetConfig();

            foreach (var game in config.FavouriteGames)
            {
                // Create a new ListBoxRow
                var row = new ListBoxRow();

                // Create a Label to display the game name
                var label = new Label(game)
                {
                    Halign = Align.Start // Align text to the left
                };

                // Add the Label to the ListBoxRow
                row.Add(label);

                // Add the ListBoxRow to the ListBox
                favGameListBox.Add(row);
            }

            // Show all the new rows
            favGameListBox.ShowAll();
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
        private Widget CreateTabPage(TwitchUser twitchUser)
        {
            //return userNotebook first page
            var page = new TwitchUserTab(twitchUser).GetFirstPage();

            return page;
        }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }
    }
}