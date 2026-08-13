namespace LSUtils.Geometry;

using LSUtils.Spatial;

/// <summary>
/// Helpers for broad-phase geometric relation checks.
/// </summary>
public static class GeometryRelations {
    public static ShapeRelation Classify(Bounds a, Bounds b) {
        if (!a.Intersects(b)) return ShapeRelation.Disjoint;
        if (a.Contains(b)) return ShapeRelation.Contains;
        if (b.Contains(a)) return ShapeRelation.ContainedBy;
        if (Touches(a, b)) return ShapeRelation.Touches;
        return ShapeRelation.Intersects;
    }

    public static ShapeRelation Classify(IShape2D a, IShape2D b) {
        return Classify(a.Bounds, b.Bounds);
    }

    private static bool Touches(Bounds a, Bounds b) {
        bool xTouches = a.MaxX == b.MinX || a.MinX == b.MaxX;
        bool yOverlaps = a.MaxY >= b.MinY && a.MinY <= b.MaxY;
        bool yTouches = a.MaxY == b.MinY || a.MinY == b.MaxY;
        bool xOverlaps = a.MaxX >= b.MinX && a.MinX <= b.MaxX;

        return (xTouches && yOverlaps) || (yTouches && xOverlaps);
    }
}
