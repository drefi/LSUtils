/**
namespace LSUtils.ECS;

public struct LSEntity : System.IComparable<LSEntity>, System.IEquatable<LSEntity> {
    public int Index { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Version { get; set; }

    public int CompareTo(LSEntity other) {
        return Index.CompareTo(other.Index);
    }
    public bool Equals(LSEntity other) {
        return Index == other.Index && Version == other.Version;
    }
    public override bool Equals(object? obj) {
        if (obj is LSEntity other) {
            return Equals(other);
        }
        return false;
    }
    public override int GetHashCode() {
        var hashCode = new System.HashCode();
        hashCode.Add(Index);
        hashCode.Add(Version);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(LSEntity left, LSEntity right) {
        return left.Equals(right);
    }

    public static bool operator !=(LSEntity left, LSEntity right) {
        return !left.Equals(right);
    }
}
**/
