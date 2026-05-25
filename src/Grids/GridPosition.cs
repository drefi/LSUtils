namespace LSUtils.Grids;

public struct GridPosition : IGridPosition {
    public int X { get; }
    public int Y { get; }
    public GridPosition(int x, int y) { X = x; Y = y; }

    public override string ToString() => $"({X}, {Y})";
    public override bool Equals(object? obj) => obj is GridPosition other && this == other;
    public override int GetHashCode() => System.HashCode.Combine(X, Y);
    public static bool operator ==(GridPosition a, GridPosition b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(GridPosition a, GridPosition b) => !(a == b);
}
