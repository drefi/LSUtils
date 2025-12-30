namespace LSUtils.Graphs;

using System.Collections.Generic;
using LSUtils.Grids;

public class GridGraphAdapter<TPosition, TData> : IGraph<TPosition> where TPosition : IGridPosition {
    private readonly IGrid<TPosition, TData> _grid;
    private readonly bool _includeDiagonals;
    
    public GridGraphAdapter(IGrid<TPosition, TData> grid, bool includeDiagonals = false) {
        _grid = grid;
        _includeDiagonals = includeDiagonals;
    }
    
    public IEnumerable<TPosition> Nodes => _grid.GetAllPositions();
    public IEnumerable<TPosition> GetNeighbors(TPosition node) => _grid.GetNeighbors(node, _includeDiagonals);
    public bool HasNode(TPosition node) => _grid.IsValidPosition(node);
}
