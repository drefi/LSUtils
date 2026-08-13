namespace LSUtils.Geometry;

/// <summary>
/// Describes the broad spatial relation between two 2D shapes or bounds.
/// </summary>
public enum ShapeRelation {
    Disjoint,
    Touches,
    Intersects,
    Contains,
    ContainedBy,
}
