using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.AvaloniaUI.ViewModels;

public class TabUserViewModel : ViewModelBase, INotifyPropertyChanged
{
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
        set { _game = value; OnPropertyChanged(); }
    }

    private string _drop;
    public string Drop
    {
        get => _drop;
        set { _drop = value; OnPropertyChanged(); }
    }

    private string _dropImage;
    public string DropImage
    {
        get => _dropImage;
        set { _dropImage = value; OnPropertyChanged(); }
    }

    private string _gameImage;
    public string GameImage
    {
        get => _gameImage;
        set { _gameImage = value; OnPropertyChanged(); }
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    private string _percentage;
    public string Percentage
    {
        get => _percentage;
        set { _percentage = value; OnPropertyChanged(); }
    }

    private string _minutesRemaining;
    public string MinutesRemaining
    {
        get => _minutesRemaining;
        set { _minutesRemaining = value; OnPropertyChanged(); }
    }

    public ObservableCollection<InventoryGameViewModel> Inventory { get; } = new();

    public TabUserViewModel(string header, object? content, string? iconUrl = null)
    {
        Header = header;
        Content = content;
        IconUrl = iconUrl;
    }

    public TabUserViewModel(TwitchUser user)
    {
        TwitchUser = user;
        Header = user.Login;
        Content = user;
        //IconUrl = user.ProfileImageUrl;
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
            var currentGame = Inventory.FirstOrDefault(game => game.GameName == TwitchUser.CurrentCampaign.Game?.DisplayName);

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

        foreach (var campaign in inventory.DropCampaignsInProgress)
        {
            // Recherche ou création du jeu dans la collection
            var gameVm = Inventory.FirstOrDefault(g => g.GameName == campaign.Game.Name);
            if (gameVm == null)
            {
                gameVm = new InventoryGameViewModel
                {
                    GameName = campaign.Game.Name,
                    GameImageUrl = campaign.Game.BoxArtURL,
                    IsCurrentGame = TwitchUser.CurrentCampaign?.Id == campaign.Id
                };
                Inventory.Add(gameVm);
            }

            // Création de la campagne
            var campaignVm = new InventoryCampaignViewModel
            {
                Name = campaign.Name
            };

            // Ajout des drops/items
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
    }
}