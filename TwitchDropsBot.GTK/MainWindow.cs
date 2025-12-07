using Gtk;
using System;
using UI = Gtk.Builder.ObjectAttribute;
using System.Runtime.InteropServices;
using System.Threading;
using TwitchDropsBot.Core;
using System.Linq;
using Microsoft.Win32;
using System.Reflection;
using Gdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Factories.User;
using TwitchDropsBot.Core.Platform.Shared.Serilog;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Settings;
using Application = Gtk.Application;
using Task = System.Threading.Tasks.Task;
using Window = Gtk.Window;
using Menu = Gtk.Menu;

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

        private StatusIcon trayIcon;
        private Menu trayMenu;
        private IOptionsMonitor<BotSettings> _botSettings;
        private ILogger<MainWindow> _logger;
        private UserFactory _userFactory;
        private SettingsManager _settingsManager;
        private IServiceProvider _serviceProvider;
        public MainWindow(ILogger<MainWindow> logger, SettingsManager manager, UserFactory userFactory, IOptionsMonitor<BotSettings> botSettings) : this(new Builder("MainWindow.glade"))
        {
            var assembly = Assembly.GetExecutingAssembly();
            Pixbuf icon = new Pixbuf(assembly, "TwitchDropsBot.GTK.images.logo.png");
            trayIcon = new StatusIcon(icon);
            trayIcon.Visible = false;
            _botSettings = botSettings;
            _logger = logger;
            _userFactory = userFactory;
            _settingsManager = manager;
            // _serviceProvider = serviceProvider;

            addAccountButton.Clicked += buttonAddNewAccount_Click;
            putInTrayButton.Clicked += (sender, args) =>
            {
                putInTray();
            };

            // Set the checkbox values
            launchOnStartupCheckbox.Active = _botSettings.CurrentValue.LaunchOnStartup;
            onlyFavouritesCheckbox.Active = _botSettings.CurrentValue.TwitchSettings.OnlyFavouriteGames;
            onlyConnectedCheckbox.Active = _botSettings.CurrentValue.TwitchSettings.OnlyConnectedAccounts;
            putInTrayCheckbox.Active = _botSettings.CurrentValue.MinimizeInTray;

            this.WindowStateEvent += (sender, args) =>
            {
                if (_botSettings.CurrentValue.MinimizeInTray && args.Event.NewWindowState == Gdk.WindowState.Iconified)
                {
                    putInTray();
                }
            };

            //if linux, disable launch on startup
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                launchOnStartupCheckbox.Sensitive = false;
            }

            while (_botSettings.CurrentValue.TwitchSettings.TwitchUsers.Count == 0)
            {
                _logger.LogInformation("No users found in the configuration file.");
                _logger.LogInformation("Login process will start.");

                // AuthDevice authDevice = new AuthDevice();
                AuthDevice authDevice = new AuthDevice(_settingsManager, botSettings, _logger);
                ResponseType response = (ResponseType)authDevice.Run();

                switch (response)
                {
                    case ResponseType.Cancel:
                        authDevice.Destroy();
                        Environment.Exit(0);
                        break;
                }
            }


            foreach (TwitchUserSettings userSettings in _botSettings.CurrentValue.TwitchSettings.TwitchUsers.Where(u => u.Enabled))
            {
                if (!userSettings.Enabled)
                {
                    _logger.LogInformation($"User {userSettings.Login} is not enabled, skipping...");
                    continue;
                }
                
                var twitchUser = _userFactory.CreateTwitchUser(userSettings, true);
                twitchUser.StartBot();
                usersNotebook.AppendPage(CreateTabPage(twitchUser), new Label(twitchUser.Login));
                usersNotebook.ShowAll();

                InitList();
            }

            InitEvents();
            InitButtons();
        }

        private void buttonAddNewAccount_Click(object sender, EventArgs e)
        {
            // AuthDevice authDevice = new AuthDevice();
            AuthDevice authDevice = new AuthDevice(_settingsManager, _botSettings, _logger);
            
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
            TwitchUserSettings userSettings = _botSettings.CurrentValue.TwitchSettings.TwitchUsers.Last();
            var twitchUser = _userFactory.CreateTwitchUser(userSettings, true);
            twitchUser.StartBot();
            usersNotebook.AppendPage(CreateTabPage(twitchUser), new Label(twitchUser.Login));
            usersNotebook.ShowAll();
        }

        private void InitEvents()
        {
            launchOnStartupCheckbox.Toggled += (sender, args) =>
            {
                var newSettings = _settingsManager.Read();
                newSettings.LaunchOnStartup = launchOnStartupCheckbox.Active;
                SetStartup(launchOnStartupCheckbox.Active);
                _settingsManager.Save(newSettings);
            };

            onlyFavouritesCheckbox.Toggled += (sender, args) =>
            {
                var newSettings = _settingsManager.Read();
                newSettings.TwitchSettings.OnlyFavouriteGames = onlyFavouritesCheckbox.Active;
                _settingsManager.Save(newSettings);
            };

            onlyConnectedCheckbox.Toggled += (sender, args) =>
            {
                var newSettings = _settingsManager.Read();
                newSettings.TwitchSettings.OnlyConnectedAccounts = onlyConnectedCheckbox.Active;
                _settingsManager.Save(newSettings);
            };

            putInTrayCheckbox.Toggled += (sender, args) =>
            {
                var newSettings = _settingsManager.Read();
                newSettings.MinimizeInTray = putInTrayCheckbox.Active;
                _settingsManager.Save(newSettings);
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

                var newSettings = _settingsManager.Read();

                if (!_botSettings.CurrentValue.FavouriteGames.Contains(gameName))
                {
                    newSettings.FavouriteGames.Add(gameName);
                }

                _settingsManager.Save(newSettings);

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

                    var newConfig = _settingsManager.Read();
                    
                    if (_botSettings.CurrentValue.FavouriteGames.Contains(gameName))
                    {
                        newConfig.FavouriteGames.Remove(gameName);
                    }

                    _settingsManager.Save(newConfig);

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
                        
                        var newSettings = _settingsManager.Read();
                        newSettings.FavouriteGames.RemoveAt(index);
                        newSettings.FavouriteGames.Insert(index - 1, gameName);
                        _settingsManager.Save(newSettings);

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

                        var newSettings = _settingsManager.Read();
                        newSettings.FavouriteGames.RemoveAt(index);
                        newSettings.FavouriteGames.Insert(index + 1, gameName);
                        _settingsManager.Save(newSettings);

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

            foreach (var game in _botSettings.CurrentValue.FavouriteGames)
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

        private void putInTray()
        {
            this.Hide();

            trayIcon.Visible = true;
            trayIcon.TooltipText = "Twitch Drops Bot";


            trayIcon.Activate += delegate
            {
                this.ShowAll();
                this.Deiconify();
                trayIcon.Visible = false;
            };

        }
    }
}