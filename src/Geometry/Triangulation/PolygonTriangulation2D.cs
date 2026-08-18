namespace LSUtils.Geometry.Triangulation;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A constrained triangulation clipped to a polygonal shape.</summary>
public sealed class PolygonTriangulationResult {
    internal PolygonTriangulationResult(
        IReadOnlyList<LSVector2> vertices,
        IReadOnlyList<TriangulationTriangle> triangles,
        IReadOnlyList<(int From, int To)> constraints) {
        Vertices = vertices;
        Triangles = triangles;
        Constraints = constraints;
    }

    public IReadOnlyList<LSVector2> Vertices { get; }
    public IReadOnlyList<TriangulationTriangle> Triangles { get; }
    public IReadOnlyList<(int From, int To)> Constraints { get; }
}

/// <summary>Triangulates simple polygons and polygonal areas with holes.</summary>
public static class PolygonTriangulation2D {
    public static PolygonTriangulationResult Triangulate(IPolygonalShape2D shape) {
        if (shape == null) throw new LSArgumentNullException(nameof(shape));
        var constraints = shape.BoundaryLoops.SelectMany(CreateLoopConstraints).ToList();
        var source = ConstrainedTriangulation2D.Triangulate(constraints);
        var triangles = source.Triangles.Where(triangle => {
            var centroid = (source.Vertices[triangle.A] + source.Vertices[triangle.B] + source.Vertices[triangle.C]) / 3f;
            return shape.Locate(centroid.X, centroid.Y) != PointLocation.Outside;
        }).ToList().AsReadOnly();
        return new PolygonTriangulationResult(source.Vertices, triangles, source.Constraints);
    }

    public static IEnumerable<TriangulationConstraint> CreateLoopConstraints(Polygon2D loop) {
        if (loop == null) throw new LSArgumentNullException(nameof(loop));
        for (int index = 0; index < loop.Vertices.Count; index++) {
            yield return new TriangulationConstraint(loop.Vertices[index], loop.Vertices[(index + 1) % loop.Vertices.Count]);
        }
    }
}
