namespace LSUtils.Geometry;

using System.Collections.Generic;

/// <summary>A polygonal area described by one outer boundary and optional holes.</summary>
public interface IPolygonalShape2D : IShape2D {
    Polygon2D OuterBoundary { get; }
    IReadOnlyList<Polygon2D> Holes { get; }
    IReadOnlyList<Polygon2D> BoundaryLoops { get; }
    PointLocation Locate(float x, float y);
}
