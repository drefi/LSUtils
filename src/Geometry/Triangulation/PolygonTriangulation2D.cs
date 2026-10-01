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
    public static PolygonTriangulationResult Triangulate(ILSPolygonalShape2D shape) {
        if (shape == null) throw new LSArgumentNullException(nameof(shape));
        if (shape is LSPolygonArea2D area && area.Holes.Count == 0) {
            if (IsStrictlyConvex(area.OuterBoundary))
                return TriangulateStrictlyConvexBoundary(area.OuterBoundary);
            return TriangulateSimpleBoundary(area.OuterBoundary);
        }
        var constraints = shape.BoundaryLoops.SelectMany(CreateLoopConstraints).ToList();
        var source = ConstrainedTriangulation2D.Triangulate(constraints);
        var triangles = source.Triangles.Where(triangle => {
            var centroid = (source.Vertices[triangle.A] + source.Vertices[triangle.B] + source.Vertices[triangle.C]) / 3f;
            return shape.Locate(centroid.X, centroid.Y) != PointLocation.Outside;
        }).ToList().AsReadOnly();
        return new PolygonTriangulationResult(source.Vertices, triangles, source.Constraints);
    }

    private static bool IsStrictlyConvex(LSPolygon2D boundary) {
        const double epsilon = 1e-8;
        var vertices = boundary.Vertices.ToArray();
        double signedArea = 0d;
        for (var index = 0; index < vertices.Length; index++) {
            var current = vertices[index];
            var next = vertices[(index + 1) % vertices.Length];
            signedArea += (double)current.X * next.Y - (double)next.X * current.Y;
        }
        if (Math.Abs(signedArea) <= epsilon) return false;
        var direction = Math.Sign(signedArea);
        for (var index = 0; index < vertices.Length; index++) {
            var cross = Cross(vertices[index], vertices[(index + 1) % vertices.Length],
                vertices[(index + 2) % vertices.Length]);
            if (direction * cross <= epsilon) return false;
        }
        return true;
    }

    private static double Cross(LSVector2 a, LSVector2 b, LSVector2 c) =>
            ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);

    private static PolygonTriangulationResult TriangulateStrictlyConvexBoundary(LSPolygon2D boundary) {
        var vertices = boundary.Vertices.ToArray();
        var counterClockwise = boundary.SignedArea > 0f;
        var triangles = new List<TriangulationTriangle>(vertices.Length - 2);
        for (var index = 1; index < vertices.Length - 1; index++)
            triangles.Add(counterClockwise
                ? new TriangulationTriangle(0, index, index + 1)
                : new TriangulationTriangle(0, index + 1, index));
        var constraints = Enumerable.Range(0, vertices.Length)
            .Select(index => index < (index + 1) % vertices.Length
                ? (index, (index + 1) % vertices.Length)
                : ((index + 1) % vertices.Length, index)).ToArray();
        return new PolygonTriangulationResult(Array.AsReadOnly(vertices), triangles.AsReadOnly(), constraints);
    }

    private static PolygonTriangulationResult TriangulateSimpleBoundary(LSPolygon2D boundary) {
        var vertices = boundary.Vertices.ToArray();
        var remaining = Enumerable.Range(0, vertices.Length).ToList();
        if (boundary.SignedArea < 0f) remaining.Reverse();

        var triangles = new List<TriangulationTriangle>(vertices.Length - 2);
        while (remaining.Count > 3) {
            var clipped = false;
            for (var index = 0; index < remaining.Count; index++) {
                var previous = remaining[(index + remaining.Count - 1) % remaining.Count];
                var current = remaining[index];
                var next = remaining[(index + 1) % remaining.Count];
                if (Cross(vertices[previous], vertices[current], vertices[next]) <= 1e-8) continue;
                if (remaining.Any(candidate => candidate != previous && candidate != current && candidate != next
                    && (StrictlyInsideTriangle(vertices[candidate], vertices[previous], vertices[current], vertices[next])
                        || OnSegment(vertices[candidate], vertices[previous], vertices[next])))) continue;

                triangles.Add(new TriangulationTriangle(previous, current, next));
                remaining.RemoveAt(index);
                clipped = true;
                break;
            }
            if (!clipped)
                throw new LSException("Could not triangulate a simple polygon boundary by ear clipping.");
        }

        triangles.Add(new TriangulationTriangle(remaining[0], remaining[1], remaining[2]));
        var constraints = Enumerable.Range(0, vertices.Length)
            .Select(index => index < (index + 1) % vertices.Length
                ? (index, (index + 1) % vertices.Length)
                : ((index + 1) % vertices.Length, index)).ToArray();
        return new PolygonTriangulationResult(Array.AsReadOnly(vertices), triangles.AsReadOnly(), constraints);
    }

    private static bool StrictlyInsideTriangle(LSVector2 point, LSVector2 a, LSVector2 b, LSVector2 c) =>
        Cross(a, b, point) > 1e-8 && Cross(b, c, point) > 1e-8 && Cross(c, a, point) > 1e-8;

    private static bool OnSegment(LSVector2 point, LSVector2 from, LSVector2 to) =>
        Math.Abs(Cross(from, to, point)) <= 1e-8
        && point.X >= Math.Min(from.X, to.X) - 1e-8 && point.X <= Math.Max(from.X, to.X) + 1e-8
        && point.Y >= Math.Min(from.Y, to.Y) - 1e-8 && point.Y <= Math.Max(from.Y, to.Y) + 1e-8;

    public static IEnumerable<TriangulationConstraint> CreateLoopConstraints(LSPolygon2D loop) {
        if (loop == null) throw new LSArgumentNullException(nameof(loop));
        for (int index = 0; index < loop.Vertices.Count; index++) {
            yield return new TriangulationConstraint(loop.Vertices[index], loop.Vertices[(index + 1) % loop.Vertices.Count]);
        }
    }
}
