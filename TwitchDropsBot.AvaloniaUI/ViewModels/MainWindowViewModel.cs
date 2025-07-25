using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace TwitchDropsBot.AvaloniaUI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {

        [ObservableProperty]
        private ViewModelBase _currentPage;

        private readonly BotViewModel _botPage = new BotViewModel();
        private readonly SettingsViewModel _SettingsPage = new SettingsViewModel();
        private readonly LoginViewModel _LoginViewModel = new LoginViewModel();

        [RelayCommand]
        private void NavigateToBotPage()
        {
            CurrentPage = _botPage;
        }

        [RelayCommand]
        private void NavigateToSettingsPage()
        {
            CurrentPage = _SettingsPage;
        }

        public MainWindowViewModel()
        {
            CurrentPage = _LoginViewModel;

        }

    }
}
