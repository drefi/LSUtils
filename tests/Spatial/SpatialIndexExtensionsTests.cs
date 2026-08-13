namespace LSUtils.Tests.Spatial;

using System;
using NUnit.Framework;
using LSUtils.Spatial;

[TestFixture]
public class SpatialIndexExtensionsTests {
    [Test]
    public void Query_ReturnsItemsIntersectingArea() {
        ISpatialIndex<TestSpatialObject> index = new SpatialHashGrid<TestSpatialObject>(10);
        var item = new TestSpatialObject(new Bounds(0, 0, 2, 2));
        index.Insert(item);

        var result = index.Query(new Bounds(0, 0, 4, 4));

        Assert.That(result, Does.Contain(item));
    }

    [Test]
    public void Query_WithMask_ExcludesMaskedItems() {
        ISpatialIndex<TestSpatialObject> index = new SpatialHashGrid<TestSpatialObject>(10);
        var item = new TestSpatialObject(new Bounds(0, 0, 2, 2));
        index.Insert(item);

        var result = SpatialIndexExtensions.Query(index, new Bounds(0, 0, 4, 4), new[] { item });

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Update_UsesCurrentObjectBounds() {
        ISpatialIndex<TestSpatialObject> index = new SpatialHashGrid<TestSpatialObject>(10);
        var item = new TestSpatialObject(new Bounds(0, 0, 2, 2));
        index.Insert(item);

        item.Bounds = new Bounds(50, 50, 2, 2);
        index.Update(item);

        Assert.That(index.Query(new Bounds(0, 0, 4, 4)), Is.Empty);
        Assert.That(index.Query(new Bounds(50, 50, 4, 4)), Does.Contain(item));
    }

    private sealed class TestSpatialObject : ISpatialObject {
        public Guid ID { get; } = Guid.NewGuid();
        public Bounds Bounds { get; set; }

        public TestSpatialObject(Bounds bounds) {
            Bounds = bounds;
        }
    }
}
