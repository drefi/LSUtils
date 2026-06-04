namespace LSUtils.Grids;

public struct GridPosition : IGridPosition {
    public int ColIndex { get; }
    public int RowIndex { get; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    /// <summary>
    /// Creates a GridPosition with the given column and row indices, and optional positional offsets. The column and row indices represent the grid coordinates, while the positional offsets can be used for more precise positioning within the grid cell if needed. If the positional offsets are not provided, they will default to 0, meaning the position will be at the top-left corner of the grid cell defined by the column and row indices.
    /// </summary>
    /// <param name="colIndex"></param>
    /// <param name="rowIndex"></param>
    public GridPosition(int colIndex, int rowIndex) : this(colIndex, rowIndex, 0f, 0f) { }
    /// <summary>
    /// Creates a GridPosition with the given column and row indices, and positional offsets. The column and row indices represent the grid coordinates, while the positional offsets can be used for more precise positioning within the grid cell if needed. This constructor allows you to specify both the grid coordinates and the exact position within that grid cell.
    /// </summary>
    /// <param name="colIndex"></param>
    /// <param name="rowIndex"></param>
    /// <param name="posX"></param>
    /// <param name="posY"></param>
    public GridPosition(int colIndex, int rowIndex, float posX, float posY) {
        ColIndex = colIndex;
        RowIndex = rowIndex;
        PosX = posX;
        PosY = posY;
    }
    /// <summary>
    /// Determines if this GridPosition has the same column and row indices as another GridPosition, regardless of their positional offsets. This can be useful for checking if two positions are in the same grid cell, even if they have different precise positions within that cell.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool SameIndexAs(GridPosition other) => ColIndex == other.ColIndex && RowIndex == other.RowIndex;
    public override string ToString() => $"({ColIndex}, {RowIndex}, {PosX}, {PosY})";
    public override bool Equals(object? obj) => obj is GridPosition other && this == other;
    public override int GetHashCode() => System.HashCode.Combine(ColIndex, RowIndex, PosX, PosY);
    public static bool operator ==(GridPosition a, GridPosition b) => a.ColIndex == b.ColIndex && a.RowIndex == b.RowIndex && a.PosX == b.PosX && a.PosY == b.PosY;
    public static bool operator !=(GridPosition a, GridPosition b) => !(a == b);

}
