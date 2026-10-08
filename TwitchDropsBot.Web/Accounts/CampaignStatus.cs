using TwitchDropsBot.Core.Platform.Twitch.Models;

namespace TwitchDropsBot.Web.Accounts;

public sealed record TierStatus(string Name, string? ImageUrl, int Current, int Required, bool Claimed);

/// <summary>
/// One account's progress on one campaign, per drop. Twitch drops a fully claimed campaign from the
/// inventory, so for those only a live channel's progress (or an earlier check) says it is done.
/// </summary>
public sealed record CampaignStatus(
    string CampaignId,
    IReadOnlyList<TierStatus> Tiers,
    DateTime CheckedAt,
    string Source,
    string? Channel = null)
{
    public bool AllClaimed => Tiers.Count > 0 && Tiers.All(x => x.Claimed);

    /// <summary>Drops watched to the end but not claimed, usually because the game account isn't linked.</summary>
    public int Waiting => Tiers.Count(x => !x.Claimed && x.Required > 0 && x.Current >= x.Required);

    public bool Started => Tiers.Any(x => x.Claimed || x.Current > 0);

    /// <summary>From DropChannelCampaignsProgress, which reports every reward group as CLAIMED or not.</summary>
    public static CampaignStatus FromChannel(DropsCampaign campaign, string? channel)
    {
        var tiers = (campaign.RewardGroups ?? new List<DropsRewardGroup>())
            .Where(x => (x.ProgressCriteria?.Requirements?.MinutesWatched ?? 0) > 0)
            .OrderBy(x => x.ProgressCriteria!.Requirements!.MinutesWatched)
            .Select(x =>
            {
                var required = x.ProgressCriteria!.Requirements!.MinutesWatched ?? 0;
                return new TierStatus(
                    x.Name ?? "Drop",
                    x.Rewards?.FirstOrDefault()?.ThumbnailURL,
                    Math.Min(x.Self?.CurrentMinutesWatched ?? 0, required),
                    required,
                    string.Equals(x.Self?.Status, "CLAIMED", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        return new CampaignStatus(campaign.Id, tiers, DateTime.UtcNow, "channel", channel);
    }

    /// <summary>From an Inventory campaign in progress, which carries minutes and claims per drop.</summary>
    public static CampaignStatus FromInventory(DropCampaign campaign, DateTime at)
    {
        var tiers = campaign.TimeBasedDrops
            .Where(x => x.RequiredMinutesWatched > 0)
            .OrderBy(x => x.RequiredMinutesWatched)
            .Select(x =>
            {
                var benefit = x.BenefitEdges.FirstOrDefault()?.Benefit;
                return new TierStatus(
                    benefit?.Name ?? x.Name ?? "Drop",
                    benefit?.ImageAssetURL,
                    Math.Min(x.Self?.CurrentMinutesWatched ?? 0, x.RequiredMinutesWatched),
                    x.RequiredMinutesWatched,
                    x.Self?.IsClaimed == true);
            })
            .ToList();

        return new CampaignStatus(campaign.Id, tiers, at, "inventory");
    }
}
