namespace LSUtils.Grids;

using System.Collections.Generic;
public interface IGrid<TPosition, TData> where TPosition : IGridPosition {
    int Width { get; }
    int Height { get; }
    TData? GetCell(TPosition pos);
    bool SetCell(TPosition pos, TData value);
    bool IsValidPosition(TPosition pos);
    IEnumerable<TPosition> GetNeighbors(TPosition pos, bool includeDiagonals = false);
    IEnumerable<TPosition> GetAllPositions();
    void Clear();
}
