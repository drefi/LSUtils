namespace LSUtils.Tests.Spatial;

using System;
using System.Linq;
using LSUtils.Spatial;
using NUnit.Framework;

[TestFixture]
public class SpatialHashGridTests {
    [Test]
    public void Constructor_InvalidCellSize_ThrowsException() {
        Assert.Throws<ArgumentException>(() => new SpatialHashGrid<string>(0));
        Assert.Throws<ArgumentException>(() => new SpatialHashGrid<string>(-1));
    }

    [Test]
    public void Insert_AndQuery_ThroughInterface_ReturnsMatchingItems() {
        ISpatialIndex<string> spatialIndex = new SpatialHashGrid<string>(10);

        bool inserted = spatialIndex.Insert("Item1", new Bounds(12, 12, 4, 4));
        var results = spatialIndex.Query(new Bounds(10, 10, 10, 10));

        Assert.That(inserted, Is.True);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results, Does.Contain("Item1"));
    }

    [Test]
    public void Query_ItemSpanningMultipleCells_ReturnsItemOnce() {
        var grid = new SpatialHashGrid<string>(10);

        grid.Insert("LargeItem", new Bounds(10, 10, 18, 18));

        var results = grid.Query(new Bounds(10, 10, 40, 40));

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results.Count(item => item == "LargeItem"), Is.EqualTo(1));
    }

    [Test]
    public void Update_ExistingItem_MovesItemToNewArea() {
        ISpatialIndex<string> spatialIndex = new SpatialHashGrid<string>(10);
        spatialIndex.Insert("Item1", new Bounds(5, 5, 4, 4));

        bool updated = spatialIndex.Update("Item1", new Bounds(5, 5, 4, 4), new Bounds(35, 35, 4, 4));

        Assert.That(updated, Is.True);
        Assert.That(spatialIndex.Query(new Bounds(5, 5, 10, 10)), Does.Not.Contain("Item1"));
        Assert.That(spatialIndex.Query(new Bounds(35, 35, 10, 10)), Does.Contain("Item1"));
    }

    [Test]
    public void Remove_ExistingItem_ReturnsTrue() {
        var grid = new SpatialHashGrid<string>(10);
        grid.Insert("Item1", new Bounds(5, 5, 4, 4));

        bool removed = grid.Remove("Item1");

        Assert.That(removed, Is.True);
        Assert.That(grid.Count, Is.EqualTo(0));
    }

    [Test]
    public void Clear_RemovesAllItems() {
        var grid = new SpatialHashGrid<string>(10);
        grid.Insert("Item1", new Bounds(5, 5, 4, 4));
        grid.Insert("Item2", new Bounds(25, 25, 4, 4));

        grid.Clear();

        Assert.That(grid.Count, Is.EqualTo(0));
        Assert.That(grid.Query(new Bounds(15, 15, 50, 50)), Is.Empty);
    }
}
