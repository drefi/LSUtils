namespace LSUtils.Geometry;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Spatial;

/// <summary>
/// A simple immutable 2D polygon backed by ordered vertices.
/// </summary>
public sealed class Polygon2D : IShape2D {
    private readonly List<LSVector2> _vertices;

    public IReadOnlyList<LSVector2> Vertices => _vertices;
    public Bounds Bounds { get; }
    public float Area { get; }

    public Polygon2D(IEnumerable<ILSVector2> vertices) {
        if (vertices == null) throw new LSArgumentNullException(nameof(vertices));

        _vertices = vertices.Select(v => new LSVector2(v)).ToList();
        if (_vertices.Count < 3) throw new LSArgumentException("A polygon needs at least 3 vertices.", nameof(vertices));

        Bounds = CalculateBounds(_vertices);
        Area = CalculateArea(_vertices);
    }

    public Polygon2D(IEnumerable<LSVector2> vertices) {
        if (vertices == null) throw new LSArgumentNullException(nameof(vertices));

        _vertices = vertices.ToList();
        if (_vertices.Count < 3) throw new LSArgumentException("A polygon needs at least 3 vertices.", nameof(vertices));

        Bounds = CalculateBounds(_vertices);
        Area = CalculateArea(_vertices);
    }

    public bool Contains(float x, float y) {
        if (!Bounds.Contains(x, y)) return false;

        bool inside = false;
        int previous = _vertices.Count - 1;

        for (int current = 0; current < _vertices.Count; current++) {
            var a = _vertices[current];
            var b = _vertices[previous];

            if ((a.Y > y) != (b.Y > y) &&
                x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y + float.Epsilon) + a.X) {
                inside = !inside;
            }

            previous = current;
        }

        return inside;
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

    private static float CalculateArea(IReadOnlyList<LSVector2> vertices) {
        float area = 0f;

        for (int i = 0; i < vertices.Count; i++) {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            area += a.X * b.Y - b.X * a.Y;
        }

        return LSMath.Abs(area) * 0.5f;
    }
}
