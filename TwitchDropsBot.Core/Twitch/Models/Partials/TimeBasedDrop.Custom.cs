using System.Text.RegularExpressions;
using TwitchDropsBot.Core.Twitch.Models.Interfaces;

namespace TwitchDropsBot.Core.Twitch.Models;

public partial class TimeBasedDrop : IInventorySystem
{
    public Game Game { get; set; }
    
    public string GetName()
    {
        return BenefitEdges[0].Benefit.Name;
    }

    public string GetImage()
    {
        return BenefitEdges[0].Benefit.ImageAssetURL;
    }

    public string GetGroup()
    {
        return Game?.DisplayName ?? Game?.Name ?? "Unknown";
    }

    public string GetStatus()
    {
        return IsClaimed() ? "\u2714" : "\u26A0";
    }

    public string? GetGameImageUrl(int size)
    {
        var url = Game?.BoxArtUrl;

        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        url = Regex.Replace(url, @"\d+x\d+", $"{size}x{size}");

        return url;
    }

    public string? GetGameSlug()
    {
        return Game?.Slug;
    }
    
    public bool IsClaimed()
    {
        if (Self is not null)
        {
            return Self.IsClaimed;
        }
        
        return BenefitEdges.All(edge => edge.Benefit.IsClaimed);
    }
}