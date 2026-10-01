namespace LSUtils.Geometry;

using System.Collections.Generic;

/// <summary>A polygonal area described by one outer boundary and optional holes.</summary>
public interface ILSPolygonalShape2D : ILSShape2D {
    LSPolygon2D OuterBoundary { get; }
    IReadOnlyList<LSPolygon2D> Holes { get; }
    IReadOnlyList<LSPolygon2D> BoundaryLoops { get; }
    PointLocation Locate(float x, float y);
}
