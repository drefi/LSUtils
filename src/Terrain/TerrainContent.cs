namespace LSUtils.Terrain;

using LSUtils.Geometry;
using LSUtils.Spatial;

public class TerrainContent<TContentType> : ISpatialObject {
    public System.Guid ID { get; } = System.Guid.NewGuid();
    public TContentType Type { get; private set; }
    public IShape2D Shape { get; private set; }
    public TerrainContentMobility Mobility { get; private set; }
    public Bounds Bounds => Shape.Bounds;
    public float Area => Shape.Area;

    public TerrainContent(TContentType type, IShape2D shape, TerrainContentMobility mobility = TerrainContentMobility.Static) {
        Type = type;
        Shape = shape ?? throw new LSArgumentNullException(nameof(shape));
        Mobility = mobility;
    }

    public bool Contains(float x, float y) {
        return Shape.Contains(x, y);
    }

    public void SetShape(IShape2D shape) {
        Shape = shape ?? throw new LSArgumentNullException(nameof(shape));
    }

    public void SetType(TContentType type) {
        Type = type;
    }

    public void SetMobility(TerrainContentMobility mobility) {
        Mobility = mobility;
    }
}
