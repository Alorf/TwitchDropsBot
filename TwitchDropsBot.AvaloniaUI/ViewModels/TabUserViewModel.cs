using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.AvaloniaUI.ViewModels;

public partial class TabUserViewModel : ViewModelBase, INotifyPropertyChanged
{
    private BotViewModel _botPage;
    public string Header { get; set; }
    public string? IconUrl { get; set; }
    public object? Content { get; set; }

    private TwitchUser _twitchUser;

    public TwitchUser TwitchUser
    {
        get => _twitchUser;
        set
        {
            if (_twitchUser != value)
            {
                if (_twitchUser != null)
                    _twitchUser.PropertyChanged -= TwitchUser_PropertyChanged;

                _twitchUser = value;
                if (_twitchUser != null)
                    _twitchUser.PropertyChanged += TwitchUser_PropertyChanged;

                OnPropertyChanged();
                UpdateFromTwitchUser();
            }
        }
    }

    private string _game;

    public string Game
    {
        get => _game;
        set
        {
            _game = value;
            OnPropertyChanged();
        }
    }

    private string _drop;

    public string Drop
    {
        get => _drop;
        set
        {
            _drop = value;
            OnPropertyChanged();
        }
    }

    private string _dropImage;

    public string DropImage
    {
        get => _dropImage;
        set
        {
            _dropImage = value;
            OnPropertyChanged();
        }
    }

    private string _gameImage;

    public string GameImage
    {
        get => _gameImage;
        set
        {
            _gameImage = value;
            OnPropertyChanged();
        }
    }

    private int _progress;

    public int Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            OnPropertyChanged();
        }
    }

    private string _percentage;

    public string Percentage
    {
        get => _percentage;
        set
        {
            _percentage = value;
            OnPropertyChanged();
        }
    }

    private string _minutesRemaining;

    public string MinutesRemaining
    {
        get => _minutesRemaining;
        set
        {
            _minutesRemaining = value;
            OnPropertyChanged();
        }
    }

    private string _logs = "";

    public string Logs
    {
        get => _logs;
        set
        {
            _logs = value;
            OnPropertyChanged();
        }
    }
    
    public ObservableCollection<InventoryGameViewModel> Inventory { get; } = new();

    public event Action<TabUserViewModel>? OnUserDeleteRequested;

    public TabUserViewModel(string header, object? content, string? iconUrl = null)
    {
        Header = header;
        Content = content;
        IconUrl = iconUrl;
    }

    public TabUserViewModel(TwitchUser user, BotViewModel botPage)
    {
        _botPage = botPage;
        TwitchUser = user;
        Header = user.Login;
        Content = user;
        //IconUrl = user.ProfileImageUrl;

        TwitchUser.Logger.OnLog += (message) => AppendLog($"LOG: {message}");
        TwitchUser.Logger.OnError += (message) => AppendLog($"ERROR: {message}");
        TwitchUser.Logger.OnException += (exception) => AppendLog($"ERROR: {exception.ToString()}");
        TwitchUser.Logger.OnInfo += (message) => AppendLog($"INFO: {message}");
    }

    public void AppendLog(string message)
    {
        Logs += message + Environment.NewLine;
        OnPropertyChanged(nameof(Logs));
    }
    
    [RelayCommand]
    private async Task ReloadUserAsync()
    {
        if (_twitchUser.CancellationTokenSource != null &&
            !_twitchUser.CancellationTokenSource.IsCancellationRequested)
        {
            _twitchUser.ReloadBot = true;
            _twitchUser.Logger.Info("Reload requested");
            _twitchUser.CancellationTokenSource?.Cancel();
        }
    }
    
    [RelayCommand]
    private async Task DeleteUserAsync()
    {
        var dialogResult = await MessageBoxManager.GetMessageBoxStandard(
            "Need Confirmation",
            $"Are you sure you want to delete the user {TwitchUser.Login}?",
            ButtonEnum.YesNo,
            Icon.Warning).ShowAsync();
        
        if (dialogResult == ButtonResult.Yes)
        {
            _twitchUser.Logger.Info("User deletion requested");
            _twitchUser.CancellationTokenSource?.Cancel();
            _twitchUser.ReloadBot = false;
            _botPage.RemoveTab(this);
            AppConfig.Instance.RemoveUserBySecret(_twitchUser.ClientSecret);
        }
    }

    private void TwitchUser_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TwitchUser.Status):
                UpdateFromTwitchUser();
                break;
            case nameof(TwitchUser.CurrentDropCurrentSession):
                UpdateProgress();
                break;
            case nameof(TwitchUser.Inventory):
                InitInventory();
                break;
        }
    }

    private void UpdateFromTwitchUser()
    {
        Game = TwitchUser.CurrentCampaign?.Game.Name ?? "N/A";
        Drop = TwitchUser.CurrentTimeBasedDrop?.Name ?? "N/A";
        DropImage = TwitchUser.CurrentTimeBasedDrop?.GetImage() ?? "N/A";
        //update IsCurrentGame in InventoryGameViewModel
        if (TwitchUser.CurrentCampaign != null)
        {
            var currentGame =
                Inventory.FirstOrDefault(game => game.GameName == TwitchUser.CurrentCampaign.Game?.DisplayName);

            if (currentGame != null) currentGame.IsCurrentGame = true;
        }
        else
        {
            foreach (var gameVm in Inventory)
            {
                gameVm.IsCurrentGame = false;
            }
        }

        UpdateProgress();
    }

    private void UpdateProgress()
    {
        var session = TwitchUser.CurrentDropCurrentSession;
        if (session != null && session.requiredMinutesWatched > 0)
        {
            Progress = (int)((session.CurrentMinutesWatched / (double)session.requiredMinutesWatched) * 100);
            if (Progress > 100) Progress = 100;
            Percentage = $"{Progress}%";
            MinutesRemaining = $"Minutes remaining : {session.requiredMinutesWatched - session.CurrentMinutesWatched}";
        }
        else
        {
            Progress = 0;
            Percentage = "-%";
            MinutesRemaining = "Minutes remaining : -";
        }
    }

    public async void InitInventory()
    {
        Inventory.Clear();

        var inventory = await _twitchUser.GqlRequest.FetchInventoryDropsAsync();
        if (inventory?.DropCampaignsInProgress == null)
            return;

        var config = AppConfig.Instance;
        var favoriteGameNames = config.FavouriteGames ?? new List<string>();

        var tempGamesList = new List<InventoryGameViewModel>();

        foreach (var campaign in inventory.DropCampaignsInProgress)
        {
            // Search or create game view model
            var gameVm = tempGamesList.FirstOrDefault(g => g.GameName == campaign.Game.Name);
            if (gameVm == null)
            {
                gameVm = new InventoryGameViewModel
                {
                    GameName = campaign.Game.Name,
                    GameImageUrl = campaign.Game.BoxArtURL,
                    IsCurrentGame = TwitchUser.CurrentCampaign?.Id == campaign.Id
                };
                tempGamesList.Add(gameVm);
            }

            // Create Campaign
            var campaignVm = new InventoryCampaignViewModel
            {
                Name = campaign.Name
            };

            // Add drops/items
            foreach (var drop in campaign.TimeBasedDrops)
            {
                campaignVm.Items.Add(new InventoryItemViewModel
                {
                    Title = drop.Name,
                    ImageUrl = drop.GetImage()
                });
            }

            gameVm.Items.Add(campaignVm);
        }

        // Sort inventory games based on favorite games
        var sortedGames = tempGamesList
            .OrderBy(game =>
                favoriteGameNames.IndexOf(game.GameName) == -1
                    ? int.MaxValue
                    : favoriteGameNames.IndexOf(game.GameName));
        
        //todo : Add an "Claimed" section
        var claimedDrops = inventory.GameEventDrops;

        if (claimedDrops != null)
        {
            //Create a section for claimed drops
            var claimedGameVm = new InventoryGameViewModel();
            claimedGameVm.GameName = "Claimed Drops";
            claimedGameVm.GameImageUrl = "https://static-cdn.jtvnw.net/ttv-static/404_boxart.jpg";
            claimedGameVm.IsCurrentGame = false;
            foreach (var drop in claimedDrops)
            {
                var campaignVm = new InventoryItemViewModel
                {
                    Title = drop.Name,
                    ImageUrl = drop.GetImage()
                };

                claimedGameVm.Items.Add(campaignVm);
            }
            
            Inventory.Add(claimedGameVm);
        }
        
        foreach (var game in sortedGames)
        {
            Inventory.Add(game);
        }
    }
}