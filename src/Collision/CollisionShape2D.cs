namespace LSUtils.Collision;

using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Geometry;
using LSUtils.Spatial;

public enum CollisionShapeKind {
    Circle,
    Rectangle,
    Polygon
}

/// <summary>
/// Engine-agnostic primitive used by gameplay collision queries.
/// Rectangles are axis-aligned in this first implementation.
/// </summary>
public readonly record struct CollisionShape2D(
    CollisionShapeKind Kind,
    LSVector2 Center,
    float Radius,
    LSVector2 Size,
    IReadOnlyList<LSVector2>? Vertices = null,
    IReadOnlyList<IReadOnlyList<LSVector2>>? BoundaryLoops = null) {
    public static CollisionShape2D Circle(LSVector2 center, float radius) {
        if (radius < 0f || !float.IsFinite(radius)) throw new LSArgumentException("Circle radius must be finite and non-negative.");
        return new(CollisionShapeKind.Circle, center, radius, LSVector2.Zero);
    }

    public static CollisionShape2D Rectangle(LSVector2 center, LSVector2 size) {
        if (size.X < 0f || size.Y < 0f || !float.IsFinite(size.X) || !float.IsFinite(size.Y))
            throw new LSArgumentException("Rectangle size must be finite and non-negative.");
        return new(CollisionShapeKind.Rectangle, center, 0f, size);
    }

    public static CollisionShape2D Polygon(IEnumerable<LSVector2> vertices) {
        if (vertices == null) throw new LSArgumentNullException(nameof(vertices));
        var points = new List<LSVector2>(vertices);
        if (points.Count < 3) throw new LSArgumentException("A collision polygon needs at least 3 vertices.", nameof(vertices));
        var loop = points.AsReadOnly();
        return new(CollisionShapeKind.Polygon, CalculateCenter(points), 0f, LSVector2.Zero, loop, new[] { loop });
    }

    public static CollisionShape2D PolygonWithHoles(IEnumerable<IEnumerable<LSVector2>> boundaryLoops) {
        if (boundaryLoops == null) throw new LSArgumentNullException(nameof(boundaryLoops));
        var loops = boundaryLoops.Select(loop => {
            var points = new List<LSVector2>(loop ?? throw new LSArgumentException("Collision boundary loops cannot be null."));
            if (points.Count < 3) throw new LSArgumentException("A collision boundary needs at least 3 vertices.", nameof(boundaryLoops));
            return (IReadOnlyList<LSVector2>)points.AsReadOnly();
        }).ToList();
        if (loops.Count == 0) throw new LSArgumentException("A collision polygon needs an outer boundary.", nameof(boundaryLoops));
        return new(CollisionShapeKind.Polygon, CalculateCenter(loops[0]), 0f, LSVector2.Zero, loops[0], loops.AsReadOnly());
    }

    public Bounds Bounds => Kind == CollisionShapeKind.Circle
        ? new Bounds(Center.X, Center.Y, Radius * 2f, Radius * 2f)
        : Kind == CollisionShapeKind.Rectangle
            ? new Bounds(Center.X, Center.Y, Size.X, Size.Y)
            : CalculateBounds(BoundaryLoops ?? new[] { Vertices! });

    private static LSVector2 CalculateCenter(IReadOnlyList<LSVector2> vertices) {
        float x = 0f;
        float y = 0f;
        foreach (var vertex in vertices) {
            x += vertex.X;
            y += vertex.Y;
        }
        return new LSVector2(x / vertices.Count, y / vertices.Count);
    }

    private static Bounds CalculateBounds(IReadOnlyList<IReadOnlyList<LSVector2>> loops) {
        float minX = loops[0][0].X;
        float maxX = minX;
        float minY = loops[0][0].Y;
        float maxY = minY;
        foreach (var loop in loops) foreach (var vertex in loop) {
            minX = MathF.Min(minX, vertex.X);
            maxX = MathF.Max(maxX, vertex.X);
            minY = MathF.Min(minY, vertex.Y);
            maxY = MathF.Max(maxY, vertex.Y);
        }
        return new Bounds((minX + maxX) / 2f, (minY + maxY) / 2f, maxX - minX, maxY - minY);
    }
}
