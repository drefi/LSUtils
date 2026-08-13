namespace LSUtils.Tests.Spatial;

using System;
using System.Linq;
using NUnit.Framework;
using LSUtils.Spatial;
using System.Collections.Generic;

[TestFixture]
public class QuadTreeTests {
    [Test]
    public void Constructor_ValidParameters_CreatesQuadTree() {
        var bounds = new Bounds(0, 0, 100, 100);
        var quadTree = new QuadTree<string>(16, bounds, 4);

        Assert.That(quadTree.Bounds, Is.EqualTo(bounds));
        Assert.That(quadTree.Capacity, Is.EqualTo(4));
        Assert.That(quadTree.Count, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_InvalidCapacity_ThrowsException() {
        var bounds = new Bounds(0, 0, 100, 100);

        Assert.Throws<LSArgumentException>(() => new QuadTree<string>(16, bounds, 0));
        Assert.Throws<LSArgumentException>(() => new QuadTree<string>(16, bounds, -1));
    }

    [Test]
    public void Insert_SingleItem_ReturnsTrue() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));
        var itemBounds = new Bounds(10, 10, 5, 5);

        bool result = quadTree.Insert("Item1", itemBounds);

        Assert.That(result, Is.True);
        Assert.That(quadTree.Count, Is.EqualTo(1));
    }

    [Test]
    public void Insert_ItemOutsideBounds_ReturnsFalse() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));
        var itemBounds = new Bounds(200, 200, 5, 5);

        bool result = quadTree.Insert("OutsideItem", itemBounds);

        Assert.That(result, Is.False);
        Assert.That(quadTree.Count, Is.EqualTo(0));
    }

    [Test]
    public void Insert_MultipleItemsWithinCapacity_StoresInSameNode() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100), 4);

        quadTree.Insert("Item1", new Bounds(10, 10, 5, 5));
        quadTree.Insert("Item2", new Bounds(20, 20, 5, 5));
        quadTree.Insert("Item3", new Bounds(30, 30, 5, 5));

        Assert.That(quadTree.Count, Is.EqualTo(3));
    }

    [Test]
    public void Insert_ExceedsCapacity_Subdivides() {
        var quadTree = new QuadTree<int>(16, new Bounds(0, 0, 100, 100), 2);

        quadTree.Insert(1, new Bounds(-20, -20, 5, 5));
        quadTree.Insert(2, new Bounds(20, -20, 5, 5));
        quadTree.Insert(3, new Bounds(-20, 20, 5, 5));
        quadTree.Insert(4, new Bounds(20, 20, 5, 5));

        Assert.That(quadTree.Count, Is.EqualTo(4));
    }

    [Test]
    public void Query_EmptyTree_ReturnsEmpty() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));
        var searchArea = new Bounds(0, 0, 50, 50);
        List<string> results = new();
        quadTree.Query(searchArea, results);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Query_ItemsInArea_ReturnsMatchingItems() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));

        quadTree.Insert("Item1", new Bounds(10, 10, 5, 5));
        quadTree.Insert("Item2", new Bounds(80, 80, 5, 5));
        quadTree.Insert("Item3", new Bounds(18, 18, 5, 5));

        var searchArea = new Bounds(10, 10, 20, 20);
        List<string> results = new();
        quadTree.Query(searchArea, results);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results, Does.Contain("Item1"));
        Assert.That(results, Does.Contain("Item3"));
        Assert.That(results, Does.Not.Contain("Item2"));
    }

    [Test]
    public void Query_NoIntersection_ReturnsEmpty() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));

        quadTree.Insert("Item1", new Bounds(-40, -40, 5, 5));
        quadTree.Insert("Item2", new Bounds(-30, -30, 5, 5));

        var searchArea = new Bounds(40, 40, 20, 20);
        List<string> results = new();
        quadTree.Query(searchArea, results);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Query_AfterSubdivision_ReturnsCorrectItems() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100), 2);

        quadTree.Insert("NW", new Bounds(-20, -20, 5, 5));
        quadTree.Insert("NE", new Bounds(20, -20, 5, 5));
        quadTree.Insert("SW", new Bounds(-20, 20, 5, 5));
        quadTree.Insert("SE", new Bounds(20, 20, 5, 5));

        var searchNW = new Bounds(-25, -25, 20, 20);
        List<string> resultsNW = new();
        quadTree.Query(searchNW, resultsNW);

        Assert.That(resultsNW, Has.Count.EqualTo(1));
        Assert.That(resultsNW, Does.Contain("NW"));
    }

    [Test]
    public void Query_ItemSpanningMultipleQuadrants_ReturnsItemOnce() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100), 1);

        quadTree.Insert("LargeItem", new Bounds(0, 0, 60, 60));
        quadTree.Insert("OtherItem", new Bounds(40, 40, 5, 5));

        var searchArea = new Bounds(0, 0, 100, 100);
        List<string> results = new();
        quadTree.Query(searchArea, results);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results, Does.Contain("OtherItem"));
        Assert.That(results, Does.Contain("LargeItem"));
    }

    [Test]
    public void Remove_ExistingItem_ReturnsTrue() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));
        quadTree.Insert("Item1", new Bounds(10, 10, 5, 5));

        bool removed = quadTree.Remove("Item1");

        Assert.That(removed, Is.True);
        Assert.That(quadTree.Count, Is.EqualTo(0));
    }

    [Test]
    public void Remove_NonExistingItem_ReturnsFalse() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));
        quadTree.Insert("Item1", new Bounds(10, 10, 5, 5));

        bool removed = quadTree.Remove("Item2");

        Assert.That(removed, Is.False);
        Assert.That(quadTree.Count, Is.EqualTo(1));
    }

    [Test]
    public void Remove_ItemFromSubdividedTree_ReturnsTrue() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100), 2);

        quadTree.Insert("Item1", new Bounds(-20, -20, 5, 5));
        quadTree.Insert("Item2", new Bounds(20, -20, 5, 5));
        quadTree.Insert("Item3", new Bounds(-20, 20, 5, 5));

        bool removed = quadTree.Remove("Item2");

        Assert.That(removed, Is.True);
        Assert.That(quadTree.Count, Is.EqualTo(2));

        var searchArea = new Bounds(0, 0, 100, 100);
        List<string> results = new();
        quadTree.Query(searchArea, results);

        Assert.That(results, Does.Not.Contain("Item2"));
    }

    [Test]
    public void Update_ExistingItem_MovesItemToNewArea() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100), 1);
        var itemBounds = new Bounds(-20, -20, 5, 5);
        quadTree.Insert("Item1", itemBounds);
        var newItemBounds = new Bounds(20, 20, 5, 5);
        bool updated = quadTree.Update("Item1", newItemBounds);

        Assert.That(updated, Is.True);
        var searchArea = new Bounds(-20, -20, 10, 10);
        List<string> results = new();
        quadTree.Query(searchArea, results);
        Assert.That(results, Does.Not.Contain("Item1"));
        searchArea = new Bounds(20, 20, 10, 10);
        quadTree.Query(searchArea, results);
        Assert.That(results, Does.Contain("Item1"));
    }

    [Test]
    public void Clear_RemovesAllItems() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));

        quadTree.Insert("Item1", new Bounds(10, 10, 5, 5));
        quadTree.Insert("Item2", new Bounds(20, 20, 5, 5));
        quadTree.Insert("Item3", new Bounds(30, 30, 5, 5));

        quadTree.Clear();

        Assert.That(quadTree.Count, Is.EqualTo(0));
        var searchArea = new Bounds(0, 0, 100, 100);
        List<string> results = new();
        quadTree.Query(searchArea, results);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Insert_ManyItems_MaintainsCorrectCount() {
        var quadTree = new QuadTree<int>(16, new Bounds(0, 0, 1000, 1000), 4);
        int itemCount = 100;
        const int columns = 10;
        const float spacing = 100f;

        for (int i = 0; i < itemCount; i++) {
            float x = (i % columns) * spacing - 450f;
            float y = (i / columns) * spacing - 450f;
            quadTree.Insert(i, new Bounds(x, y, 0f, 0f));
        }

        Assert.That(quadTree.Count, Is.EqualTo(itemCount));
    }

    [Test]
    public void Query_LargeArea_ReturnsAllItems() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));

        quadTree.Insert("Item1", new Bounds(-30, -30, 5, 5));
        quadTree.Insert("Item2", new Bounds(30, -30, 5, 5));
        quadTree.Insert("Item3", new Bounds(0, 0, 5, 5));

        var searchArea = new Bounds(0, 0, 100, 100);
        List<string> results = new();
        quadTree.Query(searchArea, results);

        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public void Insert_ItemAtBoundary_HandledCorrectly() {
        var quadTree = new QuadTree<string>(16, new Bounds(0, 0, 100, 100));
        var itemBounds = new Bounds(50, 50, 5, 5); // At edge

        bool result = quadTree.Insert("EdgeItem", itemBounds);

        Assert.That(result, Is.True);
        Assert.That(quadTree.Count, Is.EqualTo(1));
    }
}
