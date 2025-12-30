namespace LSUtils.Grids;

public class GridCell<TPosition, TData> where TPosition : IGridPosition {
    public TPosition Position { get; }
    public TData Data { get; set; }
    public GridCell(TPosition position, TData data) {
        Position = position;
        Data = data;
    }
}
