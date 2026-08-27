namespace LSUtils.Collision;

using System;
using System.Collections.Generic;
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

        if (first.Kind == CollisionShapeKind.Polygon || second.Kind == CollisionShapeKind.Polygon) {
            var polygon = first.Kind == CollisionShapeKind.Polygon ? first : second;
            var other = first.Kind == CollisionShapeKind.Polygon ? second : first;
            var boundaryPoint = ClosestPointOnLoops(other.Center, polygon.BoundaryLoops!);
            if (other.Kind == CollisionShapeKind.Circle) {
                var contactDirection = other.Center.Subtract(boundaryPoint).Normalized();
                if (contactDirection.LengthSquared() <= 0.000001f) contactDirection = LSVector2.Right;
                if (first.Kind == CollisionShapeKind.Circle) {
                    normal = new LSVector2(contactDirection);
                    point = boundaryPoint;
                } else {
                    normal = new LSVector2(contactDirection * -1f);
                    point = boundaryPoint;
                }
                distance = MathF.Sqrt(DistanceSquared(other.Center, boundaryPoint));
                return true;
            }

            var centerDelta = second.Center.Subtract(first.Center).Normalized();
            if (centerDelta.LengthSquared() <= 0.000001f) centerDelta = LSVector2.Right;
            normal = new LSVector2(centerDelta);
            point = boundaryPoint;
            distance = MathF.Sqrt(DistanceSquared(first.Center, boundaryPoint));
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

        if (first.Kind == CollisionShapeKind.Polygon && second.Kind == CollisionShapeKind.Polygon)
            return PolygonsIntersect(first.BoundaryLoops!, second.BoundaryLoops!);

        if (first.Kind == CollisionShapeKind.Polygon || second.Kind == CollisionShapeKind.Polygon) {
            var polygon = first.Kind == CollisionShapeKind.Polygon ? first : second;
            var other = first.Kind == CollisionShapeKind.Polygon ? second : first;
            if (other.Kind == CollisionShapeKind.Circle)
                return CircleIntersectsPolygon(other.Center, other.Radius, polygon.BoundaryLoops!);
            var rectangleBounds = other.Bounds;
            return RectangleIntersectsPolygon(rectangleBounds, polygon.BoundaryLoops!);
        }

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

        if (target.Kind == CollisionShapeKind.Polygon)
            return TryGetSegmentPolygonFraction(from, to, radius, target.BoundaryLoops!, out fraction);

        var bounds = target.Bounds;
        var expanded = new Bounds(bounds.X, bounds.Y, bounds.Width + radius * 2f, bounds.Height + radius * 2f);
        return TryGetSegmentBoundsFraction(from, to, expanded, out fraction);
    }

    private static bool TryGetSegmentPolygonFraction(LSVector2 from, LSVector2 to, float radius,
        IReadOnlyList<IReadOnlyList<LSVector2>> loops, out float fraction) {
        fraction = 0f;
        if (PointInFilledPolygon(from, loops)) return true;
        if (PointInFilledPolygon(to, loops)) {
            fraction = 1f;
            return true;
        }

        bool found = false;
        float closest = float.PositiveInfinity;
        foreach (var polygon in loops) for (int i = 0; i < polygon.Count; i++) {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (SegmentsIntersect(from, to, a, b, out var edgeFraction)) {
                if (edgeFraction < closest) { closest = edgeFraction; found = true; }
            }
            if (radius > 0f) {
                if (TryGetSegmentCircleFraction(from, to, a, radius, out var vertexFraction)
                    && vertexFraction < closest) { closest = vertexFraction; found = true; }
                if (TryGetSegmentCapsuleFraction(from, to, a, b, radius, out var edgeCapsuleFraction)
                    && edgeCapsuleFraction < closest) { closest = edgeCapsuleFraction; found = true; }
            }
        }
        if (found) fraction = closest;
        return found;
    }

    private static bool CircleIntersectsPolygon(LSVector2 center, float radius, IReadOnlyList<IReadOnlyList<LSVector2>> loops) {
        if (PointInFilledPolygon(center, loops)) return true;
        var radiusSquared = radius * radius;
        foreach (var polygon in loops) for (int i = 0; i < polygon.Count; i++)
            if (DistanceSquaredToSegment(center, polygon[i], polygon[(i + 1) % polygon.Count]) <= radiusSquared) return true;
        return false;
    }

    private static bool RectangleIntersectsPolygon(Bounds rectangle, IReadOnlyList<IReadOnlyList<LSVector2>> loops) {
        var corners = new[] {
            new LSVector2(rectangle.MinX, rectangle.MinY), new LSVector2(rectangle.MaxX, rectangle.MinY),
            new LSVector2(rectangle.MaxX, rectangle.MaxY), new LSVector2(rectangle.MinX, rectangle.MaxY)
        };
        for (int i = 0; i < corners.Length; i++) if (PointInFilledPolygon(corners[i], loops)) return true;
        foreach (var polygon in loops) for (int i = 0; i < polygon.Count; i++) {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
            for (int j = 0; j < corners.Length; j++) {
                var c = corners[j]; var d = corners[(j + 1) % corners.Length];
                if (SegmentsIntersect(a, b, c, d, out _)) return true;
            }
        }
        return PointInFilledPolygon(new LSVector2(rectangle.X, rectangle.Y), loops);
    }

    private static bool PolygonsIntersect(IReadOnlyList<IReadOnlyList<LSVector2>> first, IReadOnlyList<IReadOnlyList<LSVector2>> second) {
        foreach (var firstLoop in first) foreach (var secondLoop in second)
            for (int i = 0; i < firstLoop.Count; i++) for (int j = 0; j < secondLoop.Count; j++)
                if (SegmentsIntersect(firstLoop[i], firstLoop[(i + 1) % firstLoop.Count], secondLoop[j], secondLoop[(j + 1) % secondLoop.Count], out _)) return true;
        return PointInFilledPolygon(first[0][0], second) || PointInFilledPolygon(second[0][0], first);
    }

    private static bool PointInFilledPolygon(LSVector2 point, IReadOnlyList<IReadOnlyList<LSVector2>> loops) {
        if (!PointInPolygon(point, loops[0])) return false;
        for (int i = 1; i < loops.Count; i++) if (PointInPolygon(point, loops[i])) return false;
        return true;
    }

    private static LSVector2 ClosestPointOnLoops(LSVector2 point, IReadOnlyList<IReadOnlyList<LSVector2>> loops) {
        var closest = loops[0][0];
        var closestDistance = float.PositiveInfinity;
        foreach (var loop in loops) for (int i = 0; i < loop.Count; i++) {
            var candidate = ClosestPointOnSegment(point, loop[i], loop[(i + 1) % loop.Count]);
            var distance = DistanceSquared(point, candidate);
            if (distance < closestDistance) { closestDistance = distance; closest = candidate; }
        }
        return closest;
    }

    private static LSVector2 ClosestPointOnSegment(LSVector2 point, LSVector2 from, LSVector2 to) {
        var delta = to - from;
        var lengthSquared = delta.LengthSquared();
        if (lengthSquared <= 0.000001f) return from;
        var projection = (point - from).Dot(delta) / lengthSquared;
        projection = Math.Clamp(projection, 0f, 1f);
        return from + delta * projection;
    }

    private static bool PointInPolygon(LSVector2 point, IReadOnlyList<LSVector2> polygon) {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++) {
            var a = polygon[i]; var b = polygon[j];
            if (DistanceSquaredToSegment(point, a, b) <= 0.000001f) return true;
            if ((a.Y > point.Y) != (b.Y > point.Y)
                && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    private static bool TryGetSegmentCapsuleFraction(LSVector2 from, LSVector2 to, LSVector2 a, LSVector2 b,
        float radius, out float fraction) {
        fraction = 0f;
        var edge = b - a;
        var length = edge.Length();
        if (length <= 0.000001f) return false;
        var normal = new LSVector2(-edge.Y / length, edge.X / length);
        bool found = false;
        float closest = float.PositiveInfinity;
        foreach (var offset in new[] { -radius, radius }) {
            var start = from + normal * offset;
            var end = to + normal * offset;
            if (SegmentsIntersect(start, end, a, b, out var value) && value < closest) { closest = value; found = true; }
        }
        if (found) fraction = closest;
        return found;
    }

    private static bool SegmentsIntersect(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d, out float fraction) {
        var r = b - a; var s = d - c;
        var denominator = r.X * s.Y - r.Y * s.X;
        if (MathF.Abs(denominator) <= 0.000001f) { fraction = 0f; return false; }
        var delta = c - a;
        var t = (delta.X * s.Y - delta.Y * s.X) / denominator;
        var u = (delta.X * r.Y - delta.Y * r.X) / denominator;
        fraction = t;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
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
