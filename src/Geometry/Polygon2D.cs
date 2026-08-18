namespace LSUtils.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Spatial;

/// <summary>
/// A simple immutable 2D polygon backed by ordered vertices.
/// </summary>
public sealed class Polygon2D : IPolygonalShape2D {
    private const float BoundaryEpsilon = 0.00001f;
    private readonly List<LSVector2> _vertices;
    private readonly IReadOnlyList<Polygon2D> _boundaryLoops;

    public IReadOnlyList<LSVector2> Vertices => _vertices;
    public Bounds Bounds { get; }
    public float Area { get; }
    public float SignedArea { get; }
    public bool IsClockwise => SignedArea < 0f;
    public bool IsConvex => CalculateIsConvex(_vertices);
    public Polygon2D OuterBoundary => this;
    public IReadOnlyList<Polygon2D> Holes => System.Array.Empty<Polygon2D>();
    public IReadOnlyList<Polygon2D> BoundaryLoops => _boundaryLoops;

    public Polygon2D(IEnumerable<ILSVector2> vertices) {
        if (vertices == null) throw new LSArgumentNullException(nameof(vertices));

        _vertices = vertices.Select(v => new LSVector2(v)).ToList();
        if (_vertices.Count < 3) throw new LSArgumentException("A polygon needs at least 3 vertices.", nameof(vertices));

        Bounds = CalculateBounds(_vertices);
        SignedArea = CalculateSignedArea(_vertices);
        Area = LSMath.Abs(SignedArea);
        _boundaryLoops = new[] { this };
    }

    public Polygon2D(IEnumerable<LSVector2> vertices) {
        if (vertices == null) throw new LSArgumentNullException(nameof(vertices));

        _vertices = vertices.ToList();
        if (_vertices.Count < 3) throw new LSArgumentException("A polygon needs at least 3 vertices.", nameof(vertices));

        Bounds = CalculateBounds(_vertices);
        SignedArea = CalculateSignedArea(_vertices);
        Area = LSMath.Abs(SignedArea);
        _boundaryLoops = new[] { this };
    }

    public bool Contains(float x, float y) {
        return Locate(x, y) != PointLocation.Outside;
    }

    public PointLocation Locate(float x, float y) {
        if (!Bounds.Contains(x, y)) return PointLocation.Outside;

        bool inside = false;
        int previous = _vertices.Count - 1;

        for (int current = 0; current < _vertices.Count; current++) {
            var a = _vertices[current];
            var b = _vertices[previous];

            if (PointIsOnSegment(x, y, a, b)) return PointLocation.Boundary;

            if ((a.Y > y) != (b.Y > y) &&
                x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y + float.Epsilon) + a.X) {
                inside = !inside;
            }

            previous = current;
        }

        return inside ? PointLocation.Inside : PointLocation.Outside;
    }

    private static bool PointIsOnSegment(float x, float y, LSVector2 a, LSVector2 b) {
        var point = new LSVector2(x, y);
        var segment = b - a;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= BoundaryEpsilon * BoundaryEpsilon) return point.DistanceTo(a) <= BoundaryEpsilon;

        float projection = (point - a).Dot(segment) / lengthSquared;
        if (projection < -BoundaryEpsilon || projection > 1f + BoundaryEpsilon) return false;
        var closest = a + segment * Math.Clamp(projection, 0f, 1f);
        return (point - closest).LengthSquared() <= BoundaryEpsilon * BoundaryEpsilon;
    }

    private static Bounds CalculateBounds(IReadOnlyList<LSVector2> vertices) {
        float minX = vertices[0].X;
        float maxX = vertices[0].X;
        float minY = vertices[0].Y;
        float maxY = vertices[0].Y;

        for (int i = 1; i < vertices.Count; i++) {
            var vertex = vertices[i];
            if (vertex.X < minX) minX = vertex.X;
            if (vertex.X > maxX) maxX = vertex.X;
            if (vertex.Y < minY) minY = vertex.Y;
            if (vertex.Y > maxY) maxY = vertex.Y;
        }

        float width = maxX - minX;
        float height = maxY - minY;
        return new Bounds(minX + width * 0.5f, minY + height * 0.5f, width, height);
    }

    private static float CalculateSignedArea(IReadOnlyList<LSVector2> vertices) {
        float area = 0f;

        for (int i = 0; i < vertices.Count; i++) {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            area += a.X * b.Y - b.X * a.Y;
        }

        return area * 0.5f;
    }

    private static bool CalculateIsConvex(IReadOnlyList<LSVector2> vertices) {
        bool? hasPositiveTurn = null;

        for (int index = 0; index < vertices.Count; index++) {
            var previous = vertices[(index + vertices.Count - 1) % vertices.Count];
            var current = vertices[index];
            var next = vertices[(index + 1) % vertices.Count];
            float cross = (current - previous).Cross(next - current);
            if (LSMath.Abs(cross) <= float.Epsilon) continue;

            bool positiveTurn = cross > 0f;
            if (hasPositiveTurn.HasValue && hasPositiveTurn.Value != positiveTurn) return false;
            hasPositiveTurn = positiveTurn;
        }

        return hasPositiveTurn.HasValue;
    }
}
