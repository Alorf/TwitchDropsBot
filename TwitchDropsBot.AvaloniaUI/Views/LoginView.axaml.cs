using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Newtonsoft.Json.Linq;
using System;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Utilities;

namespace TwitchDropsBot.AvaloniaUI.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }
}