namespace LSUtils.Tests.Collision;

using System.Collections.Generic;
using LSUtils.Collision;
using NUnit.Framework;

[TestFixture]
public sealed class Collision2DTests {
    [Test]
    public void CircleAndRectangle_IntersectOnlyWhenShapesOverlap() {
        var rectangle = CollisionShape2D.Rectangle(new LSVector2(10f, 0f), new LSVector2(4f, 4f));

        Assert.That(Collision2D.Intersects(CollisionShape2D.Circle(new LSVector2(7f, 0f), 1f), rectangle), Is.True);
        Assert.That(Collision2D.Intersects(CollisionShape2D.Circle(new LSVector2(0f, 0f), 1f), rectangle), Is.False);
    }

    [Test]
    public void SweepCircle_DetectsCollisionBetweenDiscretePositions() {
        var obstacle = CollisionShape2D.Circle(new LSVector2(5f, 0f), 1f);

        Assert.That(Collision2D.SweepCircle(new LSVector2(0f, 0f), new LSVector2(10f, 0f), 0.5f, obstacle), Is.True);
        Assert.That(Collision2D.SweepCircle(new LSVector2(0f, 4f), new LSVector2(10f, 4f), 0.5f, obstacle), Is.False);
    }

    [Test]
    public void ContactQuery_ReturnsNormalAndDistance() {
        var first = CollisionShape2D.Circle(LSVector2.Zero, 3f);
        var second = CollisionShape2D.Circle(new LSVector2(5f, 0f), 3f);

        Assert.That(Collision2D.TryGetContact(first, second, out _, out var normal, out var distance), Is.True);
        Assert.That(normal.X, Is.EqualTo(1f).Within(0.001f));
        Assert.That(normal.Y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(distance, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void CollisionWorld_UsesSpatialIndexAndFiltersLayers() {
        var world = new CollisionWorld<string>(10f);
        var enemyFilter = new CollisionFilter(2u, 1u);
        var allyFilter = new CollisionFilter(1u, 2u);
        world.Add("enemy", CollisionShape2D.Circle(new LSVector2(5f, 0f), 2f), enemyFilter);
        world.Add("ally", CollisionShape2D.Circle(new LSVector2(5f, 5f), 2f), allyFilter);

        var hits = new List<string>();
        world.QueryOverlap(CollisionShape2D.Circle(LSVector2.Zero, 10f), hits, allyFilter);

        Assert.That(hits, Is.EquivalentTo(new[] { "enemy" }));
        Assert.That(world.TrySweepCircle(LSVector2.Zero, new LSVector2(10f, 0f), 1f, allyFilter, out var hit), Is.True);
        Assert.That(hit, Is.EqualTo("enemy"));
    }

    [Test]
    public void Polygon_IntersectsCircleAndOtherPolygon() {
        var triangle = CollisionShape2D.Polygon(new[] {
            new LSVector2(-4f, -3f), new LSVector2(4f, -3f), new LSVector2(0f, 4f)
        });
        var overlapping = CollisionShape2D.Polygon(new[] {
            new LSVector2(0f, 0f), new LSVector2(8f, 0f), new LSVector2(4f, 6f)
        });
        var separate = CollisionShape2D.Polygon(new[] {
            new LSVector2(20f, 20f), new LSVector2(24f, 20f), new LSVector2(20f, 24f)
        });

        Assert.That(Collision2D.Intersects(CollisionShape2D.Circle(LSVector2.Zero, 0.5f), triangle), Is.True);
        Assert.That(Collision2D.Intersects(triangle, overlapping), Is.True);
        Assert.That(Collision2D.Intersects(triangle, separate), Is.False);
    }

    [Test]
    public void SweepCircle_DetectsPolygonEdgeAndIgnoresClearPath() {
        var obstacle = CollisionShape2D.Polygon(new[] {
            new LSVector2(-4f, -4f), new LSVector2(4f, -4f),
            new LSVector2(4f, 4f), new LSVector2(-4f, 4f)
        });

        Assert.That(Collision2D.SweepCircle(new LSVector2(-10f, 0f), new LSVector2(10f, 0f), 1f, obstacle), Is.True);
        Assert.That(Collision2D.SweepCircle(new LSVector2(-10f, 7f), new LSVector2(10f, 7f), 1f, obstacle), Is.False);
    }

    [Test]
    public void PolygonWithHole_IsFreeInsideHoleButStillBlocksItsBoundary() {
        var shape = CollisionShape2D.PolygonWithHoles(new[] {
            new[] { new LSVector2(-10f, -10f), new LSVector2(10f, -10f), new LSVector2(10f, 10f), new LSVector2(-10f, 10f) },
            new[] { new LSVector2(-3f, -3f), new LSVector2(3f, -3f), new LSVector2(3f, 3f), new LSVector2(-3f, 3f) }
        });

        Assert.That(Collision2D.Intersects(CollisionShape2D.Circle(LSVector2.Zero, 1f), shape), Is.False);
        Assert.That(Collision2D.Intersects(CollisionShape2D.Circle(new LSVector2(8f, 0f), 1f), shape), Is.True);
        Assert.That(Collision2D.SweepCircle(new LSVector2(-8f, 0f), new LSVector2(8f, 0f), 0.5f, shape), Is.True);
    }

    [Test]
    public void ContactQuery_UsesPolygonBoundaryInsteadOfItsBounds() {
        var triangle = CollisionShape2D.Polygon(new[] {
            new LSVector2(0f, -4f), new LSVector2(4f, 4f), new LSVector2(-4f, 4f)
        });
        var circle = CollisionShape2D.Circle(new LSVector2(0f, 3.5f), 1f);

        Assert.That(Collision2D.TryGetContact(circle, triangle, out var point, out _, out _), Is.True);
        Assert.That(point.Y, Is.EqualTo(4f).Within(0.001f));
    }
}
