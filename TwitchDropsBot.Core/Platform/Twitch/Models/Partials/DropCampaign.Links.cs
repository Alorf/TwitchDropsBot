namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class DropCampaign
{
    /// <summary>
    /// Whether the campaign's rewards need a linked game account. Twitch reports
    /// isAccountConnected false for campaigns that need no link at all; their link URL is
    /// twitch.tv itself (with or without www and a trailing slash) or missing.
    /// </summary>
    public bool RequiresAccountLink()
    {
        if (string.IsNullOrWhiteSpace(AccountLinkURL))
        {
            return false;
        }

        if (!Uri.TryCreate(AccountLinkURL, UriKind.Absolute, out var uri))
        {
            return true;
        }

        var twitchHome = uri.Host.Equals("twitch.tv", StringComparison.OrdinalIgnoreCase) ||
                         uri.Host.Equals("www.twitch.tv", StringComparison.OrdinalIgnoreCase);
        return !(twitchHome && uri.AbsolutePath.Trim('/').Length == 0);
    }

    /// <summary>Whether claimed rewards can reach the account: it is linked, or no link is needed.</summary>
    public bool CanReceiveRewards() => Self?.IsAccountConnected == true || !RequiresAccountLink();
}
