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

        bool inserted = spatialIndex.Insert("Item1", new Bounds(12, 12, 4, 4));
        HashSet<string> hits = new();
        spatialIndex.Query(new Bounds(10, 10, 10, 10), hits);

        Assert.That(inserted, Is.True);
        Assert.That(hits, Has.Count.EqualTo(1));
        Assert.That(hits, Does.Contain("Item1"));
    }

    [Test]
    public void Query_ItemSpanningMultipleCells_ReturnsItemOnce() {
        var grid = new SpatialHashGrid<string>(10);

        var result = grid.Insert("LargeItem", new Bounds(10, 10, 18, 18));
        Assert.That(result, Is.True);
        HashSet<string> hits = new();
        grid.Query(new Bounds(10, 10, 40, 40), hits);

        Assert.That(hits, Has.Count.EqualTo(1));
        Assert.That(hits.Count(item => item == "LargeItem"), Is.EqualTo(1));
    }

    [Test]
    public void Remove_ExistingItem_ReturnsTrue() {
        var grid = new SpatialHashGrid<string>(10);
        var itemBounds = new Bounds(5, 5, 4, 4);
        grid.Insert("Item1", itemBounds);

        bool removed = grid.Remove("Item1");

        Assert.That(removed, Is.True);
        Assert.That(grid.Count, Is.EqualTo(0));
    }

    [Test]
    public void Update_WithinSameCells_UpdatesBoundsWithoutChangingRegistration() {
        var grid = new SpatialHashGrid<string>(10);
        grid.Insert("Item1", new Bounds(5, 5, 4, 4));

        bool updated = grid.Update("Item1", new Bounds(7, 7, 4, 4));
        HashSet<string> oldAreaHits = new();
        HashSet<string> newAreaHits = new();
        grid.Query(new Bounds(3, 3, 1, 1), oldAreaHits);
        grid.Query(new Bounds(7, 7, 1, 1), newAreaHits);

        Assert.That(updated, Is.True);
        Assert.That(grid.Count, Is.EqualTo(1));
        Assert.That(oldAreaHits, Is.Empty);
        Assert.That(newAreaHits, Does.Contain("Item1"));
        Assert.That(grid.GetBounds("Item1"), Is.EqualTo(new Bounds(7, 7, 4, 4)));
    }

    [Test]
    public void Update_AcrossCells_RemovesOldCellReferencesAndAddsNewOnes() {
        var grid = new SpatialHashGrid<string>(10);
        grid.Insert("Item1", new Bounds(5, 5, 8, 8));

        bool updated = grid.Update("Item1", new Bounds(35, 25, 8, 8));
        HashSet<string> oldAreaHits = new();
        HashSet<string> newAreaHits = new();
        grid.Query(new Bounds(5, 5, 8, 8), oldAreaHits);
        grid.Query(new Bounds(35, 25, 8, 8), newAreaHits);

        Assert.That(updated, Is.True);
        Assert.That(oldAreaHits, Is.Empty);
        Assert.That(newAreaHits, Does.Contain("Item1"));
    }

    [Test]
    public void Update_MissingItem_ReturnsFalseAndDoesNotInsert() {
        var grid = new SpatialHashGrid<string>(10);

        bool updated = grid.Update("Missing", new Bounds(5, 5, 4, 4));

        Assert.That(updated, Is.False);
        Assert.That(grid.Count, Is.Zero);
    }

    [Test]
    public void Clear_RemovesAllItems() {
        var grid = new SpatialHashGrid<string>(10);
        grid.Insert("Item1", new Bounds(5, 5, 4, 4));
        grid.Insert("Item2", new Bounds(25, 25, 4, 4));

        grid.Clear();

        Assert.That(grid.Count, Is.EqualTo(0));
        HashSet<string> hits = new();
        grid.Query(new Bounds(15, 15, 50, 50), hits);
        Assert.That(hits, Is.Empty);
    }
}
