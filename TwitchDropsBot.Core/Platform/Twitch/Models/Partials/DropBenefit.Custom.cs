using System.Text.Json.Serialization;
using TwitchDropsBot.Core.Twitch.Models.Interfaces;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class DropBenefit
{
    public bool IsClaimed { get; set; }
}