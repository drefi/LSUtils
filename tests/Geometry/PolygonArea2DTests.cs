namespace LSUtils.Tests.Geometry;

using System.Linq;
using LSUtils.Geometry;
using NUnit.Framework;

[TestFixture]
public class PolygonArea2DTests {
    [Test]
    public void AreaAndContains_SubtractInnerRings() {
        var area = new PolygonArea2D(Square(0, 0, 10), new[] { Square(3, 3, 4) });

        Assert.That(area.Area, Is.EqualTo(84f).Within(0.001f));
        Assert.That(area.Contains(1, 1), Is.True);
        Assert.That(area.Contains(5, 5), Is.False);
        Assert.That(area.Locate(3, 5), Is.EqualTo(PointLocation.Boundary));
        Assert.That(area.Bounds, Is.EqualTo(Square(0, 0, 10).Bounds));
    }

    [Test]
    public void Constructor_NormalizesBoundaryWinding() {
        var area = new PolygonArea2D(
            new Polygon2D(Square(0, 0, 10).Vertices.Reverse()),
            new[] { Square(3, 3, 4) });

        Assert.That(area.OuterBoundary.IsClockwise, Is.False);
        Assert.That(area.Holes.Single().IsClockwise, Is.True);
        Assert.That(area.BoundaryLoops, Has.Count.EqualTo(2));
    }

    [Test]
    public void Constructor_RejectsHoleOutsideOrTouchingOuterBoundary() {
        Assert.Throws<LSArgumentException>(() => new PolygonArea2D(Square(0, 0, 10), new[] { Square(8, 3, 4) }));
        Assert.Throws<LSArgumentException>(() => new PolygonArea2D(Square(0, 0, 10), new[] { Square(0, 3, 4) }));
    }

    [Test]
    public void Constructor_RejectsOverlappingAndNestedHoles() {
        Assert.Throws<LSArgumentException>(() => new PolygonArea2D(
            Square(0, 0, 20),
            new[] { Square(2, 2, 8), Square(6, 6, 8) }));
        Assert.Throws<LSArgumentException>(() => new PolygonArea2D(
            Square(0, 0, 20),
            new[] { Square(2, 2, 12), Square(5, 5, 2) }));
    }

    [Test]
    public void Constructor_RejectsSelfIntersectingBoundary() {
        var bowTie = new Polygon2D(new[] {
            new LSVector2(0, 0), new LSVector2(10, 10),
            new LSVector2(0, 10), new LSVector2(10, 0),
        });

        Assert.Throws<LSArgumentException>(() => new PolygonArea2D(bowTie));
    }

    [Test]
    public void ImmutableHoleOperations_RevalidateAndPreserveOriginalArea() {
        var original = new PolygonArea2D(Square(0, 0, 20));
        var withHole = original.WithHole(Square(3, 3, 4));
        var replaced = withHole.WithHole(0, Square(4, 4, 6));
        var removed = replaced.WithoutHole(0);

        Assert.That(original.Holes, Is.Empty);
        Assert.That(withHole.Area, Is.EqualTo(384f).Within(0.001f));
        Assert.That(replaced.Area, Is.EqualTo(364f).Within(0.001f));
        Assert.That(removed.Area, Is.EqualTo(400f).Within(0.001f));
    }

    private static Polygon2D Square(float x, float y, float size) {
        return new Polygon2D(new[] {
            new LSVector2(x, y), new LSVector2(x + size, y),
            new LSVector2(x + size, y + size), new LSVector2(x, y + size),
        });
    }
}
