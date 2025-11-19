namespace TwitchDropsBot.Core.Platform.Kick.Models;

public partial class Campaign
{
    protected bool Equals(Campaign other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Campaign)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}