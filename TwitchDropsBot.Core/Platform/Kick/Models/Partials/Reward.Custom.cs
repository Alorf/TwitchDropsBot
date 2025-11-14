namespace TwitchDropsBot.Core.Platform.Kick.Models;

public partial class Reward : IEquatable<Reward>
{
    public bool Equals(Reward? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Reward)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}