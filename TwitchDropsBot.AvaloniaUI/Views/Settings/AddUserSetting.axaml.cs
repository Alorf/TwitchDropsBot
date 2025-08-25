using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TwitchDropsBot.AvaloniaUI.Views.Settings;

public partial class AddUserSetting : UserControl
{
    public AddUserSetting()
    {
        InitializeComponent();
        this.DetachedFromVisualTree += OnDetachedFromVisualTree;
        this.AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is TwitchDropsBot.AvaloniaUI.ViewModels.LoginViewModel vm)
        {
            vm.CancelAuthentication();
        }
    }
    
    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is TwitchDropsBot.AvaloniaUI.ViewModels.LoginViewModel vm)
        {
            vm.ResetState();
        }
    }
}