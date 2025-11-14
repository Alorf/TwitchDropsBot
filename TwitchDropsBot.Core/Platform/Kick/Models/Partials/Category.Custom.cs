using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Kick.Models;
public partial class Category
{
    [JsonIgnore]
    public bool IsFavorite { get; set; }

    private sealed class IdEqualityComparer : IEqualityComparer<Category>
    {
        public bool Equals(Category? x, Category? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Id == y.Id;
        }

        public int GetHashCode(Category obj)
        {
            return obj.Id;
        }
    }

    public static IEqualityComparer<Category> IdComparer { get; } = new IdEqualityComparer();
}