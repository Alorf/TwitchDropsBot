using System;
using Avalonia.Utilities;
using System.Collections.ObjectModel;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;

namespace TwitchDropsBot.AvaloniaUI.ViewModels;

public partial class BotViewModel : ViewModelBase
{
    public ObservableCollection<TabUserViewModel> Tabs { get; set; } = new();
    private readonly AppConfig config;


    public BotViewModel()
    {
        config = AppConfig.Instance;

        /*while (config.Users.Count == 0)
        {
            SystemLogger.Info("No users found in the configuration file.");
            SystemLogger.Info("Login process will start.");

            AuthDevice authDevice = new AuthDevice();
            authDevice.ShowDialog();

            if (authDevice.DialogResult == DialogResult.Cancel)
            {
                Environment.Exit(1);
            }
        }*/


        
        foreach (ConfigUser user in config.Users)
        {
            TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId);
            twitchUser.DiscordWebhookURl = config.WebhookURL;

            Bot.StartBot(twitchUser);
            var tabUserViewModel = new TabUserViewModel(twitchUser);
            Tabs.Add(tabUserViewModel);
            tabUserViewModel.InitInventory();


        }

    }

}