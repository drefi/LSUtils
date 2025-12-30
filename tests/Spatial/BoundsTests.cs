namespace LSUtils.Tests.Spatial;

using NUnit.Framework;
using LSUtils.Spatial;
[TestFixture]
public class BoundsTests {
    [Test]
    public void Constructor_CreatesValidBounds() {
        var bounds = new Bounds(10, 20, 30, 40);

        Assert.That(bounds.X, Is.EqualTo(10));
        Assert.That(bounds.Y, Is.EqualTo(20));
        Assert.That(bounds.Width, Is.EqualTo(30));
        Assert.That(bounds.Height, Is.EqualTo(40));
    }

    [Test]
    public void MinMaxProperties_CalculateCorrectly() {
        var bounds = new Bounds(0, 0, 100, 50);

        Assert.That(bounds.MinX, Is.EqualTo(-50));
        Assert.That(bounds.MaxX, Is.EqualTo(50));
        Assert.That(bounds.MinY, Is.EqualTo(-25));
        Assert.That(bounds.MaxY, Is.EqualTo(25));
    }

    [Test]
    public void Contains_Point_ReturnsTrue() {
        var bounds = new Bounds(0, 0, 100, 100);

        Assert.That(bounds.Contains(0, 0), Is.True);
        Assert.That(bounds.Contains(25, 25), Is.True);
        Assert.That(bounds.Contains(-25, -25), Is.True);
    }

    [Test]
    public void Contains_PointOutside_ReturnsFalse() {
        var bounds = new Bounds(0, 0, 100, 100);

        Assert.That(bounds.Contains(60, 0), Is.False);
        Assert.That(bounds.Contains(0, 60), Is.False);
        Assert.That(bounds.Contains(-60, 0), Is.False);
    }

    [Test]
    public void Contains_Bounds_ReturnsTrue() {
        var outer = new Bounds(0, 0, 100, 100);
        var inner = new Bounds(0, 0, 50, 50);

        Assert.That(outer.Contains(inner), Is.True);
    }

    [Test]
    public void Contains_BoundsPartiallyOutside_ReturnsFalse() {
        var bounds1 = new Bounds(0, 0, 100, 100);
        var bounds2 = new Bounds(40, 40, 50, 50);

        Assert.That(bounds1.Contains(bounds2), Is.False);
    }

    [Test]
    public void Intersects_OverlappingBounds_ReturnsTrue() {
        var bounds1 = new Bounds(0, 0, 100, 100);
        var bounds2 = new Bounds(30, 30, 100, 100);

        Assert.That(bounds1.Intersects(bounds2), Is.True);
        Assert.That(bounds2.Intersects(bounds1), Is.True);
    }

    [Test]
    public void Intersects_NonOverlappingBounds_ReturnsFalse() {
        var bounds1 = new Bounds(0, 0, 50, 50);
        var bounds2 = new Bounds(100, 100, 50, 50);

        Assert.That(bounds1.Intersects(bounds2), Is.False);
        Assert.That(bounds2.Intersects(bounds1), Is.False);
    }

    [Test]
    public void Intersects_TouchingBounds_ReturnsTrue() {
        var bounds1 = new Bounds(0, 0, 50, 50);
        var bounds2 = new Bounds(50, 0, 50, 50);

        Assert.That(bounds1.Intersects(bounds2), Is.True);
    }

    [Test]
    public void Equality_SameBounds_ReturnsTrue() {
        var bounds1 = new Bounds(10, 20, 30, 40);
        var bounds2 = new Bounds(10, 20, 30, 40);

        Assert.That(bounds1, Is.EqualTo(bounds2));
        Assert.That(bounds1 == bounds2, Is.True);
    }

    [Test]
    public void Equality_DifferentBounds_ReturnsFalse() {
        var bounds1 = new Bounds(10, 20, 30, 40);
        var bounds2 = new Bounds(10, 20, 31, 40);

        Assert.That(bounds1, Is.Not.EqualTo(bounds2));
        Assert.That(bounds1 == bounds2, Is.False);
    }
}
