using System.Text.Json.Serialization;
using TwitchDropsBot.Core.Twitch.Models.Interfaces;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class UserDropReward : IInventorySystem
{

    public string GetName()
    {
        return Name;
    }

    public string GetImage()
    {
        return ImageURL;
    }

    public string GetGroup()
    {
        return "Inventory";
    }

    public string GetStatus()
    {
        return IsConnected ? "\u2714" : "\u26A0";
    }

    public string? GetGameImageUrl(int size)
    {
        return null;
    }

    public string? GetGameSlug()
    {
        return "Inventory";
    }
}