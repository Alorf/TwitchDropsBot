using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDropsBot.Core.Object.Config;


namespace TwitchDropsBot.AvaloniaUI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty] private ViewModelBase _currentPage;

        private readonly BotViewModel _botPage;
        private readonly SettingsViewModel _settingsPage;

        [RelayCommand]
        private void NavigateToBotPage()
        {
            CurrentPage = _botPage;
        }

        [RelayCommand]
        private void NavigateToSettingsPage()
        {
            CurrentPage = _settingsPage;
        }

        public MainWindowViewModel()
        {
            _botPage = new BotViewModel();
            _settingsPage = new SettingsViewModel(_botPage);

            AppConfig config = AppConfig.Instance;
            
            // Default page
            CurrentPage = config.Users.Count > 0 ? _botPage : _settingsPage;
        }
    }
}