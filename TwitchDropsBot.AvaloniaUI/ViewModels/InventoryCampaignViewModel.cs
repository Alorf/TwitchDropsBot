using System.Collections.ObjectModel;

namespace TwitchDropsBot.AvaloniaUI.ViewModels;

public class InventoryCampaignViewModel
{
    public string Name { get; set; }
    public ObservableCollection<object> Items { get; set; } = new(); // Item
}