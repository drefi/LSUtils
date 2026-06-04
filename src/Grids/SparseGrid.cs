namespace LSUtils.Grids;

using System.Collections.Generic;
public class SparseGrid<TData> : IGrid<GridPosition, TData> {
    private readonly Dictionary<(int, int), TData?> _cells = new();
    public int Width { get; }
    public int Height { get; }

    public SparseGrid(int width, int height) {
        Width = width;
        Height = height;
    }

    public TData? GetCell(GridPosition pos) => IsValidPosition(pos) && _cells.TryGetValue((pos.ColIndex, pos.RowIndex), out var v) ? v : default;
    public bool SetCell(GridPosition pos, TData value) {
        if (!IsValidPosition(pos)) return false;
        _cells[(pos.ColIndex, pos.RowIndex)] = value;
        return true;
    }
    public bool IsValidPosition(GridPosition pos) => pos.ColIndex >= 0 && pos.ColIndex < Width && pos.RowIndex >= 0 && pos.RowIndex < Height;
    public IEnumerable<GridPosition> GetNeighbors(GridPosition pos, bool includeDiagonals = false) {
        var directions = includeDiagonals
            ? new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) }
            : new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };
        
        foreach (var (dx, dy) in directions) {
            int nx = pos.ColIndex + dx;
            int ny = pos.RowIndex + dy;
            var npos = new GridPosition(nx, ny);
            if (IsValidPosition(npos)) yield return npos;
        }
    }
    public IEnumerable<GridPosition> GetAllPositions() {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                yield return new GridPosition(x, y);
    }
    public void Clear() => _cells.Clear();
}
