namespace TwitchDropsBot.Core.Platform.Twitch.Models;

public partial class CompletedRewardCampaigns
{
    protected bool Equals(CompletedRewardCampaigns other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((CompletedRewardCampaigns)obj);
    }

    public override int GetHashCode()
    {
        return (Id != null ? Id.GetHashCode() : 0);
    }
}