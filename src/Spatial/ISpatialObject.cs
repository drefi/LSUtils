namespace LSUtils.Spatial;

/// <summary>
/// Represents an object that exposes spatial bounds for indexing and queries.
/// </summary>
public interface ISpatialObject {
    System.Guid ID { get; }
    Bounds Bounds { get; }
}
