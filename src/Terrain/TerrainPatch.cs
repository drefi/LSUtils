namespace LSUtils.Terrain;

using LSUtils.Geometry;
using LSUtils.Spatial;

public class TerrainPatch<TTerrainType> : ISpatialObject {
    public System.Guid ID { get; } = System.Guid.NewGuid();
    public TTerrainType Type { get; private set; }
    public IShape2D Shape { get; private set; }
    public int Layer { get; private set; }
    public int Priority { get; private set; }
    public Bounds Bounds => Shape.Bounds;
    public float Area => Shape.Area;

    public TerrainPatch(TTerrainType type, IShape2D shape, int layer = 0, int priority = 0) {
        Type = type;
        Shape = shape ?? throw new LSArgumentNullException(nameof(shape));
        Layer = layer;
        Priority = priority;
    }

    public bool Contains(float x, float y) {
        return Shape.Contains(x, y);
    }

    public void SetShape(IShape2D shape) {
        Shape = shape ?? throw new LSArgumentNullException(nameof(shape));
    }

    public void SetType(TTerrainType type) {
        Type = type;
    }

    public void SetLayer(int layer) {
        Layer = layer;
    }

    public void SetPriority(int priority) {
        Priority = priority;
    }
}
