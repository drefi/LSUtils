namespace LSUtils.Tests.Geometry;

using NUnit.Framework;
using LSUtils.Geometry;
using LSUtils.Spatial;

[TestFixture]
public class GeometryRelationsTests {
    [Test]
    public void Classify_DisjointBounds_ReturnsDisjoint() {
        var a = new Bounds(0, 0, 2, 2);
        var b = new Bounds(4, 0, 2, 2);

        Assert.That(GeometryRelations.Classify(a, b), Is.EqualTo(ShapeRelation.Disjoint));
    }

    [Test]
    public void Classify_ContainedBounds_ReturnsContains() {
        var outer = new Bounds(0, 0, 10, 10);
        var inner = new Bounds(0, 0, 2, 2);

        Assert.That(GeometryRelations.Classify(outer, inner), Is.EqualTo(ShapeRelation.Contains));
    }

    [Test]
    public void Classify_ContainingBounds_ReturnsContainedBy() {
        var inner = new Bounds(0, 0, 2, 2);
        var outer = new Bounds(0, 0, 10, 10);

        Assert.That(GeometryRelations.Classify(inner, outer), Is.EqualTo(ShapeRelation.ContainedBy));
    }

    [Test]
    public void Classify_TouchingBounds_ReturnsTouches() {
        var a = new Bounds(0, 0, 2, 2);
        var b = new Bounds(2, 0, 2, 2);

        Assert.That(GeometryRelations.Classify(a, b), Is.EqualTo(ShapeRelation.Touches));
    }

    [Test]
    public void Classify_OverlappingBounds_ReturnsIntersects() {
        var a = new Bounds(0, 0, 4, 4);
        var b = new Bounds(1, 1, 4, 4);

        Assert.That(GeometryRelations.Classify(a, b), Is.EqualTo(ShapeRelation.Intersects));
    }
}
