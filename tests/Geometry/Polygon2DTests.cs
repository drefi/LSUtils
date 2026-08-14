namespace LSUtils.Tests.Geometry;

using System;
using System.Linq;
using NUnit.Framework;
using LSUtils.Geometry;
using LSUtils.Spatial;

[TestFixture]
public class Polygon2DTests {
    [Test]
    public void Constructor_WithLessThanThreeVertices_Throws() {
        Assert.Throws<LSArgumentException>(() => new Polygon2D(new[] {
            new LSVector2(0, 0),
            new LSVector2(1, 0),
        }));
    }

    [Test]
    public void Area_ReturnsShoelaceArea() {
        var polygon = new Polygon2D(new[] {
            new LSVector2(0, 0),
            new LSVector2(4, 0),
            new LSVector2(4, 3),
            new LSVector2(0, 3),
        });

        Assert.That(polygon.Area, Is.EqualTo(12f));
    }

    [Test]
    public void Bounds_ContainsAllVertices() {
        var polygon = new Polygon2D(new[] {
            new LSVector2(-2, -1),
            new LSVector2(4, 0),
            new LSVector2(1, 5),
        });

        Assert.That(polygon.Bounds, Is.EqualTo(new Bounds(1, 2, 6, 6)));
    }

    [Test]
    public void Contains_PointInside_ReturnsTrue() {
        var polygon = new Polygon2D(new[] {
            new LSVector2(0, 0),
            new LSVector2(4, 0),
            new LSVector2(4, 4),
            new LSVector2(0, 4),
        });

        Assert.That(polygon.Contains(2, 2), Is.True);
    }

    [Test]
    public void Contains_PointOutside_ReturnsFalse() {
        var polygon = new Polygon2D(new[] {
            new LSVector2(0, 0),
            new LSVector2(4, 0),
            new LSVector2(4, 4),
            new LSVector2(0, 4),
        });

        Assert.That(polygon.Contains(5, 2), Is.False);
    }

    [Test]
    public void Contains_PointsOnVerticesAndEdges_ReturnsTrue() {
        var polygon = new Polygon2D(new[] {
            new LSVector2(0, 0), new LSVector2(4, 0),
            new LSVector2(4, 4), new LSVector2(0, 4),
        });

        Assert.That(polygon.Vertices.All(vertex => polygon.Contains(vertex.X, vertex.Y)), Is.True);
        Assert.That(polygon.Contains(2, 0), Is.True);
        Assert.That(polygon.Contains(4, 2), Is.True);
    }

    [Test]
    public void ConvexityAndWinding_DescribeThePolygonTopology() {
        var clockwise = new Polygon2D(new[] {
            new LSVector2(0, 0), new LSVector2(0, 4),
            new LSVector2(4, 4), new LSVector2(4, 0),
        });
        var concave = new Polygon2D(new[] {
            new LSVector2(0, 0), new LSVector2(4, 0), new LSVector2(2, 2),
            new LSVector2(4, 4), new LSVector2(0, 4),
        });

        Assert.That(clockwise.IsClockwise, Is.True);
        Assert.That(clockwise.IsConvex, Is.True);
        Assert.That(concave.IsConvex, Is.False);
    }
}
