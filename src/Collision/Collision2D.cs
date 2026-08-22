namespace LSUtils.Collision;

using System;
using LSUtils.Spatial;

public static class Collision2D {
    public static bool TryGetContact(CollisionShape2D first, CollisionShape2D second, out LSVector2 point, out LSVector2 normal, out float distance) {
        point = first.Center;
        normal = LSVector2.Right;
        distance = 0f;
        if (!Intersects(first, second)) return false;

        if (first.Kind == CollisionShapeKind.Circle && second.Kind == CollisionShapeKind.Circle) {
            var delta = second.Center.Subtract(first.Center).Normalized();
            normal = new LSVector2(delta);
            point = new LSVector2(
                first.Center.X + delta.X * first.Radius,
                first.Center.Y + delta.Y * first.Radius);
            distance = MathF.Sqrt(DistanceSquared(first.Center, second.Center));
            return true;
        }

        var bounds = second.Bounds;
        var closest = new LSVector2(
            Math.Clamp(first.Center.X, bounds.MinX, bounds.MaxX),
            Math.Clamp(first.Center.Y, bounds.MinY, bounds.MaxY));
        var direction = closest.Subtract(first.Center).Normalized();
        normal = new LSVector2(direction);
        point = closest;
        distance = MathF.Sqrt(DistanceSquared(first.Center, closest));
        return true;
    }

    public static bool Intersects(CollisionShape2D first, CollisionShape2D second) {
        if (!first.Bounds.Intersects(second.Bounds)) return false;

        if (first.Kind == CollisionShapeKind.Circle && second.Kind == CollisionShapeKind.Circle) {
            return DistanceSquared(first.Center, second.Center) <= Square(first.Radius + second.Radius);
        }

        if (first.Kind == CollisionShapeKind.Rectangle && second.Kind == CollisionShapeKind.Rectangle)
            return first.Bounds.Intersects(second.Bounds);

        var circle = first.Kind == CollisionShapeKind.Circle ? first : second;
        var rectangle = first.Kind == CollisionShapeKind.Rectangle ? first : second;
        var bounds = rectangle.Bounds;
        var closestX = Math.Clamp(circle.Center.X, bounds.MinX, bounds.MaxX);
        var closestY = Math.Clamp(circle.Center.Y, bounds.MinY, bounds.MaxY);
        return DistanceSquared(circle.Center, new LSVector2(closestX, closestY)) <= Square(circle.Radius);
    }

    /// <summary>Tests a moving circle against a static shape using a swept segment.</summary>
    public static bool SweepCircle(LSVector2 from, LSVector2 to, float radius, CollisionShape2D target) {
        return TryGetSweepFraction(from, to, radius, target, out _);
    }

    /// <summary>
    /// Returns the normalized distance along a sweep at which the moving
    /// circle first touches the target. This is useful when several targets
    /// overlap the same sweep and the nearest impact must be deterministic.
    /// </summary>
    public static bool TryGetSweepFraction(LSVector2 from, LSVector2 to, float radius, CollisionShape2D target, out float fraction) {
        fraction = 0f;
        if (radius < 0f) throw new LSArgumentException("Sweep radius must be non-negative.");
        if (target.Kind == CollisionShapeKind.Circle) {
            return TryGetSegmentCircleFraction(from, to, target.Center, radius + target.Radius, out fraction);
        }

        var bounds = target.Bounds;
        var expanded = new Bounds(
            bounds.X,
            bounds.Y,
            bounds.Width + radius * 2f,
            bounds.Height + radius * 2f);
        return TryGetSegmentBoundsFraction(from, to, expanded, out fraction);
    }

    private static bool TryGetSegmentBoundsFraction(LSVector2 from, LSVector2 to, Bounds bounds, out float fraction) {
        var directionX = to.X - from.X;
        var directionY = to.Y - from.Y;
        var tMin = 0f;
        var tMax = 1f;

        if (!Clip(-directionX, from.X - bounds.MinX, ref tMin, ref tMax)
            || !Clip(directionX, bounds.MaxX - from.X, ref tMin, ref tMax)
            || !Clip(-directionY, from.Y - bounds.MinY, ref tMin, ref tMax)
            || !Clip(directionY, bounds.MaxY - from.Y, ref tMin, ref tMax)) {
            fraction = 0f;
            return false;
        }
        fraction = tMin;
        return true;
    }

    private static bool TryGetSegmentCircleFraction(LSVector2 from, LSVector2 to, LSVector2 center, float radius, out float fraction) {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var ox = from.X - center.X;
        var oy = from.Y - center.Y;
        var a = dx * dx + dy * dy;
        var radiusSquared = radius * radius;
        if (ox * ox + oy * oy <= radiusSquared) {
            fraction = 0f;
            return true;
        }
        if (a <= 0f) {
            fraction = 0f;
            return false;
        }

        var b = 2f * (ox * dx + oy * dy);
        var c = ox * ox + oy * oy - radiusSquared;
        var discriminant = b * b - 4f * a * c;
        if (discriminant < 0f) {
            fraction = 0f;
            return false;
        }
        var root = MathF.Sqrt(discriminant);
        var first = (-b - root) / (2f * a);
        var second = (-b + root) / (2f * a);
        fraction = first >= 0f && first <= 1f ? first : second;
        return fraction >= 0f && fraction <= 1f;
    }

    private static bool Clip(float p, float q, ref float tMin, ref float tMax) {
        if (p == 0f) return q >= 0f;
        var ratio = q / p;
        if (p < 0f) {
            if (ratio > tMax) return false;
            if (ratio > tMin) tMin = ratio;
        } else {
            if (ratio < tMin) return false;
            if (ratio < tMax) tMax = ratio;
        }
        return true;
    }

    private static float DistanceSquared(LSVector2 a, LSVector2 b) {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return x * x + y * y;
    }

    private static float DistanceSquaredToSegment(LSVector2 point, LSVector2 from, LSVector2 to) {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0f) return DistanceSquared(point, from);
        var t = ((point.X - from.X) * dx + (point.Y - from.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);
        return DistanceSquared(point, new LSVector2(from.X + dx * t, from.Y + dy * t));
    }

    private static float Square(float value) => value * value;
}
