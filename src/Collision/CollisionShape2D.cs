namespace LSUtils.Collision;

using System;
using LSUtils.Spatial;

public enum CollisionShapeKind {
    Circle,
    Rectangle
}

/// <summary>
/// Engine-agnostic primitive used by gameplay collision queries.
/// Rectangles are axis-aligned in this first implementation.
/// </summary>
public readonly record struct CollisionShape2D(CollisionShapeKind Kind, LSVector2 Center, float Radius, LSVector2 Size) {
    public static CollisionShape2D Circle(LSVector2 center, float radius) {
        if (radius < 0f || !float.IsFinite(radius)) throw new LSArgumentException("Circle radius must be finite and non-negative.");
        return new(CollisionShapeKind.Circle, center, radius, LSVector2.Zero);
    }

    public static CollisionShape2D Rectangle(LSVector2 center, LSVector2 size) {
        if (size.X < 0f || size.Y < 0f || !float.IsFinite(size.X) || !float.IsFinite(size.Y))
            throw new LSArgumentException("Rectangle size must be finite and non-negative.");
        return new(CollisionShapeKind.Rectangle, center, 0f, size);
    }

    public Bounds Bounds => Kind == CollisionShapeKind.Circle
        ? new Bounds(Center.X, Center.Y, Radius * 2f, Radius * 2f)
        : new Bounds(Center.X, Center.Y, Size.X, Size.Y);
}
