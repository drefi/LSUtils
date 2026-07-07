namespace LSUtils.Tests.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Spatial;
using NUnit.Framework;

[TestFixture]
public class SpatialHashGridTests {
    [Test]
    public void Constructor_InvalidCellSize_ThrowsException() {
        Assert.Throws<LSArgumentException>(() => new SpatialHashGrid<string>(0));
        Assert.Throws<LSArgumentException>(() => new SpatialHashGrid<string>(-1));
    }

    [Test]
    public void Insert_AndQuery_ThroughInterface_ReturnsMatchingItems() {
        ISpatialIndex<string> spatialIndex = new SpatialHashGrid<string>(10);

        bool inserted = spatialIndex.InsertOrUpdate("Item1", new Bounds(12, 12, 4, 4));
        HashSet<string> hits = new();
        spatialIndex.Query(new Bounds(10, 10, 10, 10), hits);

        Assert.That(inserted, Is.True);
        Assert.That(hits, Has.Count.EqualTo(1));
        Assert.That(hits, Does.Contain("Item1"));
    }

    [Test]
    public void Query_ItemSpanningMultipleCells_ReturnsItemOnce() {
        var grid = new SpatialHashGrid<string>(10);

        var result = grid.InsertOrUpdate("LargeItem", new Bounds(10, 10, 18, 18));
        Assert.That(result, Is.True);
        HashSet<string> hits = new();
        grid.Query(new Bounds(10, 10, 40, 40), hits);

        Assert.That(hits, Has.Count.EqualTo(1));
        Assert.That(hits.Count(item => item == "LargeItem"), Is.EqualTo(1));
    }

    [Test]
    public void Update_ExistingItem_MovesItemToNewArea() {
        ISpatialIndex<string> spatialIndex = new SpatialHashGrid<string>(10);
        var itemBounds = new Bounds(5, 5, 4, 4);
        spatialIndex.InsertOrUpdate("Item1", itemBounds);

        var newItemBounds = new Bounds(35, 35, 4, 4);
        bool updated = spatialIndex.InsertOrUpdate("Item1", newItemBounds);

        Assert.That(updated, Is.True);

        HashSet<string> hits = new();

        spatialIndex.Query(new Bounds(5, 5, 10, 10), hits);

        Assert.That(hits, Does.Not.Contain("Item1"));
        hits.Clear();
        spatialIndex.Query(new Bounds(35, 35, 10, 10), hits);
        Assert.That(hits, Does.Contain("Item1"));
    }

    [Test]
    public void Remove_ExistingItem_ReturnsTrue() {
        var grid = new SpatialHashGrid<string>(10);
        var itemBounds = new Bounds(5, 5, 4, 4);
        grid.InsertOrUpdate("Item1", itemBounds);

        bool removed = grid.Remove("Item1");

        Assert.That(removed, Is.True);
        Assert.That(grid.Count, Is.EqualTo(0));
    }

    [Test]
    public void Clear_RemovesAllItems() {
        var grid = new SpatialHashGrid<string>(10);
        grid.InsertOrUpdate("Item1", new Bounds(5, 5, 4, 4));
        grid.InsertOrUpdate("Item2", new Bounds(25, 25, 4, 4));

        grid.Clear();

        Assert.That(grid.Count, Is.EqualTo(0));
        HashSet<string> hits = new();
        grid.Query(new Bounds(15, 15, 50, 50), hits);
        Assert.That(hits, Is.Empty);
    }
}
