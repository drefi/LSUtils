namespace LSUtils.Tests.Collision;

using LSUtils.Collision;
using NUnit.Framework;

[TestFixture]
public sealed class CollisionSweepOrderingTests {
    [Test]
    public void SweepCircle_ReturnsNearestImpactAlongPath() {
        var world = new CollisionWorld<string>(8f);
        world.Add("near", CollisionShape2D.Circle(new LSVector2(20f, 0f), 3f), CollisionFilter.Default);
        world.Add("far", CollisionShape2D.Circle(new LSVector2(70f, 0f), 3f), CollisionFilter.Default);

        Assert.That(world.TrySweepCircle(LSVector2.Zero, new LSVector2(100f, 0f), 1f, CollisionFilter.Default, out var hit), Is.True);
        Assert.That(hit, Is.EqualTo("near"));
    }

    [Test]
    public void SweepCircle_ReportsStartInsideAsFirstImpact() {
        Assert.That(Collision2D.TryGetSweepFraction(
            new LSVector2(10f, 0f), new LSVector2(30f, 0f), 1f,
            CollisionShape2D.Circle(new LSVector2(10f, 0f), 4f), out var fraction), Is.True);
        Assert.That(fraction, Is.EqualTo(0f));
    }
}
