namespace LSUtils.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Spatial;

/// <summary>An immutable polygonal area with one outer boundary and optional holes.</summary>
public sealed class PolygonArea2D : IPolygonalShape2D {
    private const float Epsilon = 0.00001f;
    private readonly IReadOnlyList<Polygon2D> _holes;
    private readonly IReadOnlyList<Polygon2D> _boundaryLoops;

    public Polygon2D OuterBoundary { get; }
    public IReadOnlyList<Polygon2D> Holes => _holes;
    public IReadOnlyList<Polygon2D> BoundaryLoops => _boundaryLoops;
    public Bounds Bounds => OuterBoundary.Bounds;
    public float Area { get; }

    public PolygonArea2D(Polygon2D outerBoundary, IEnumerable<Polygon2D>? holes = null) {
        if (outerBoundary == null) throw new LSArgumentNullException(nameof(outerBoundary));
        var holeList = holes?.ToList() ?? new List<Polygon2D>();
        if (holeList.Any(hole => hole == null)) throw new LSArgumentException("Hole boundaries cannot be null.", nameof(holes));

        OuterBoundary = Normalize(outerBoundary, clockwise: false);
        _holes = holeList.Select(hole => Normalize(hole, clockwise: true)).ToList().AsReadOnly();
        ValidateSimple(OuterBoundary, "outer boundary");
        for (int index = 0; index < _holes.Count; index++) ValidateSimple(_holes[index], $"hole {index}");
        ValidateTopology(OuterBoundary, _holes);

        _boundaryLoops = new[] { OuterBoundary }.Concat(_holes).ToList().AsReadOnly();
        Area = OuterBoundary.Area - _holes.Sum(hole => hole.Area);
    }

    public PolygonArea2D(IEnumerable<LSVector2> outerBoundary, IEnumerable<IEnumerable<LSVector2>>? holes = null)
        : this(new Polygon2D(outerBoundary), holes?.Select(vertices => new Polygon2D(vertices))) { }

    public bool Contains(float x, float y) => Locate(x, y) != PointLocation.Outside;

    public PolygonArea2D WithHole(Polygon2D hole) {
        if (hole == null) throw new LSArgumentNullException(nameof(hole));
        return new PolygonArea2D(OuterBoundary, _holes.Concat(new[] { hole }));
    }

    public PolygonArea2D WithHole(int index, Polygon2D hole) {
        if (index < 0 || index >= _holes.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (hole == null) throw new LSArgumentNullException(nameof(hole));
        var next = _holes.ToList();
        next[index] = hole;
        return new PolygonArea2D(OuterBoundary, next);
    }

    public PolygonArea2D WithoutHole(int index) {
        if (index < 0 || index >= _holes.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return new PolygonArea2D(OuterBoundary, _holes.Where((_, holeIndex) => holeIndex != index));
    }

    public PointLocation Locate(float x, float y) {
        var outerLocation = OuterBoundary.Locate(x, y);
        if (outerLocation != PointLocation.Inside) return outerLocation;
        foreach (var hole in _holes) {
            var holeLocation = hole.Locate(x, y);
            if (holeLocation == PointLocation.Boundary) return PointLocation.Boundary;
            if (holeLocation == PointLocation.Inside) return PointLocation.Outside;
        }
        return PointLocation.Inside;
    }

    private static Polygon2D Normalize(Polygon2D polygon, bool clockwise) {
        if (polygon.IsClockwise == clockwise) return polygon;
        return new Polygon2D(polygon.Vertices.Reverse());
    }

    private static void ValidateSimple(Polygon2D polygon, string name) {
        if (polygon.Area <= Epsilon) throw new LSArgumentException($"The {name} must have a positive area.");
        var vertices = polygon.Vertices;
        for (int index = 0; index < vertices.Count; index++) {
            var from = vertices[index];
            var to = vertices[(index + 1) % vertices.Count];
            if (from.DistanceTo(to) <= Epsilon) throw new LSArgumentException($"The {name} contains a degenerate edge.");
        }

        for (int first = 0; first < vertices.Count; first++) {
            int firstNext = (first + 1) % vertices.Count;
            for (int second = first + 1; second < vertices.Count; second++) {
                int secondNext = (second + 1) % vertices.Count;
                if (first == second || firstNext == second || secondNext == first) continue;
                if (SegmentsIntersect(vertices[first], vertices[firstNext], vertices[second], vertices[secondNext])) {
                    throw new LSArgumentException($"The {name} must be a simple ring without self-intersections.");
                }
            }
        }
    }

    private static void ValidateTopology(Polygon2D outerBoundary, IReadOnlyList<Polygon2D> holes) {
        for (int index = 0; index < holes.Count; index++) {
            var hole = holes[index];
            if (hole.Vertices.Any(vertex => outerBoundary.Locate(vertex.X, vertex.Y) != PointLocation.Inside)
                || BoundariesIntersect(outerBoundary, hole)) {
                throw new LSArgumentException($"Hole {index} must be strictly inside the outer boundary.");
            }

            for (int otherIndex = 0; otherIndex < index; otherIndex++) {
                var other = holes[otherIndex];
                if (BoundariesIntersect(hole, other)
                    || hole.Locate(other.Vertices[0].X, other.Vertices[0].Y) != PointLocation.Outside
                    || other.Locate(hole.Vertices[0].X, hole.Vertices[0].Y) != PointLocation.Outside) {
                    throw new LSArgumentException($"Holes {otherIndex} and {index} cannot overlap, contain, or touch each other.");
                }
            }
        }
    }

    private static bool BoundariesIntersect(Polygon2D first, Polygon2D second) {
        for (int firstIndex = 0; firstIndex < first.Vertices.Count; firstIndex++) {
            var firstFrom = first.Vertices[firstIndex];
            var firstTo = first.Vertices[(firstIndex + 1) % first.Vertices.Count];
            for (int secondIndex = 0; secondIndex < second.Vertices.Count; secondIndex++) {
                var secondFrom = second.Vertices[secondIndex];
                var secondTo = second.Vertices[(secondIndex + 1) % second.Vertices.Count];
                if (SegmentsIntersect(firstFrom, firstTo, secondFrom, secondTo)) return true;
            }
        }
        return false;
    }

    private static bool SegmentsIntersect(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
        float abC = (b - a).Cross(c - a), abD = (b - a).Cross(d - a);
        float cdA = (d - c).Cross(a - c), cdB = (d - c).Cross(b - c);
        if (((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon))
            && ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon))) return true;
        return MathF.Abs(abC) <= Epsilon && PointOnSegment(c, a, b)
            || MathF.Abs(abD) <= Epsilon && PointOnSegment(d, a, b)
            || MathF.Abs(cdA) <= Epsilon && PointOnSegment(a, c, d)
            || MathF.Abs(cdB) <= Epsilon && PointOnSegment(b, c, d);
    }

    private static bool PointOnSegment(LSVector2 point, LSVector2 from, LSVector2 to) {
        return point.X >= MathF.Min(from.X, to.X) - Epsilon && point.X <= MathF.Max(from.X, to.X) + Epsilon
            && point.Y >= MathF.Min(from.Y, to.Y) - Epsilon && point.Y <= MathF.Max(from.Y, to.Y) + Epsilon;
    }
}
