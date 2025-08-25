using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using TwitchDropsBot.Core.Object.Config;

namespace TwitchDropsBot.AvaloniaUI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private BotViewModel _botPage;

    public LoginViewModel LoginViewModel { get; }

    private AppConfig _config;

    public AppConfig Config
    {
        get => _config;
        set
        {
            _config = value;
            OnPropertyChanged();
        }
    }

    private ObservableCollection<string> _favouriteGames;

    public ObservableCollection<string> FavouriteGames
    {
        get => _favouriteGames;
        set
        {
            _favouriteGames = value;
            OnPropertyChanged();
        }
    }

    private string _selectedGame;

    public string SelectedGame
    {
        get => _selectedGame;
        set
        {
            _selectedGame = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddGameCommand => new RelayCommand<string>(AddGame);
    public ICommand RemoveGameCommand => new RelayCommand<string>(RemoveGame);
    public ICommand MoveGameUpCommand => new RelayCommand<string>(MoveGameUp);
    public ICommand MoveGameDownCommand => new RelayCommand<string>(MoveGameDown);

    private void AddGame(string game)
    {
        if (!string.IsNullOrWhiteSpace(game))
        {
            if (FavouriteGames.Contains(game))
            {
                SelectedGame = game;
                return;
            }

            FavouriteGames.Add(game);
            Config.FavouriteGames.Add(game);
            Config.SaveConfig();
        }
    }

    private void RemoveGame(string game)
    {
        if (FavouriteGames.Contains(game))
        {
            FavouriteGames.Remove(game);
            Config.FavouriteGames.Remove(game);
            Config.SaveConfig();
        }
    }

    private void MoveGameUp(string game)
    {
        int idx = FavouriteGames.IndexOf(game);
        if (idx > 0)
        {
            FavouriteGames.Move(idx, idx - 1);
            Config.FavouriteGames.RemoveAt(idx);
            Config.FavouriteGames.Insert(idx - 1, game);
            SelectedGame = FavouriteGames[idx - 1];
            Config.SaveConfig();
        }
    }

    private void MoveGameDown(string game)
    {
        int idx = FavouriteGames.IndexOf(game);
        if (idx < FavouriteGames.Count - 1 && idx >= 0)
        {
            FavouriteGames.Move(idx, idx + 1);
            Config.FavouriteGames.RemoveAt(idx);
            Config.FavouriteGames.Insert(idx + 1, game);
            SelectedGame = FavouriteGames[idx + 1];
            Config.SaveConfig();
        }
    }

    private bool _onlyFavouriteGames;

    public bool OnlyFavouriteGames
    {
        get => _onlyFavouriteGames;
        set
        {
            _onlyFavouriteGames = value;
            Config.OnlyFavouriteGames = value;
            Config.SaveConfig();
            OnPropertyChanged();
        }
    }

    private bool _launchOnStartup;

    public bool LaunchOnStartup
    {
        get => _launchOnStartup;
        set
        {
            _launchOnStartup = value;
            Config.LaunchOnStartup = value;
            Config.SaveConfig();
            OnPropertyChanged();
        }
    }

    private bool _minimizeInTray;

    public bool MinimizeInTray
    {
        get => _minimizeInTray;
        set
        {
            _minimizeInTray = value;
            Config.MinimizeInTray = value;
            Config.SaveConfig();
            OnPropertyChanged();
        }
    }

    private bool _onlyConnectedAccounts;

    public bool OnlyConnectedAccounts
    {
        get => _onlyConnectedAccounts;
        set
        {
            _onlyConnectedAccounts = value;
            Config.OnlyConnectedAccounts = value;
            Config.SaveConfig();
            OnPropertyChanged();
        }
    }

    public SettingsViewModel(BotViewModel botPage)
    {
        _botPage = botPage;
        LoginViewModel = new LoginViewModel(botPage);

        Config = AppConfig.Instance;
        FavouriteGames = new ObservableCollection<string>(Config.FavouriteGames);
        OnlyFavouriteGames = Config.OnlyFavouriteGames;
        LaunchOnStartup = Config.LaunchOnStartup;
        MinimizeInTray = Config.MinimizeInTray;
        OnlyConnectedAccounts = Config.OnlyConnectedAccounts;
    }
}