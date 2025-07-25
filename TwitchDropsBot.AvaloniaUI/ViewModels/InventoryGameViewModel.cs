using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TwitchDropsBot.AvaloniaUI.ViewModels;

public class InventoryGameViewModel : INotifyPropertyChanged
{
    public string GameName { get; set; }
    public string GameImageUrl { get; set; }


    private bool _isCurrentGame;
    public bool IsCurrentGame
    {
        get => _isCurrentGame;
        set
        {
            _isCurrentGame = value;
            OnPropertyChanged();

        }
    }

    public ObservableCollection<object> Items { get; set; } = new(); //Campaign or Item
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}