namespace LSUtils.Grids;

public struct GridPosition : IGridPosition {
    public int X { get; }
    public int Y { get; }
    public GridPosition(int x, int y) { X = x; Y = y; }
}
