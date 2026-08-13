namespace LSUtils.Geometry;

using LSUtils.Spatial;

/// <summary>
/// Represents a 2D shape that can be queried spatially.
/// </summary>
public interface IShape2D {
    Bounds Bounds { get; }
    float Area { get; }
    bool Contains(float x, float y);
}
