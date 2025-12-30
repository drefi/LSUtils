namespace LSUtils.Tests.Grids;

using NUnit.Framework;
using LSUtils.Grids;

[TestFixture]
public class GridCellTests {
    [Test]
    public void GridCell_ShouldStorePositionAndData() {
        // Arrange
        var pos = new GridPosition(3, 4);

        // Act
        var cell = new GridCell<GridPosition, string>(pos, "test");

        // Assert
        Assert.That(cell.Position, Is.EqualTo(pos));
        Assert.That(cell.Data, Is.EqualTo("test"));
    }

    [Test]
    public void GridCell_ShouldAllowDataModification() {
        // Arrange
        var cell = new GridCell<GridPosition, int>(new GridPosition(1, 1), 10);

        // Act
        cell.Data = 20;

        // Assert
        Assert.That(cell.Data, Is.EqualTo(20));
    }

    [Test]
    public void GridCell_ShouldStoreNullData() {
        // Arrange & Act
        var cell = new GridCell<GridPosition, string?>(new GridPosition(0, 0), null);

        // Assert
        Assert.That(cell.Data, Is.Null);
    }

    [Test]
    public void GridCell_ShouldStoreDefaultValue() {
        // Arrange & Act
        var cell = new GridCell<GridPosition, int>(new GridPosition(1, 1), 0);

        // Assert
        Assert.That(cell.Data, Is.EqualTo(0));
    }

    [Test]
    public void GridCell_ShouldWorkWithReferenceTypes() {
        // Arrange
        var pos = new GridPosition(2, 3);
        var data = new System.Collections.Generic.List<int> { 1, 2, 3 };

        // Act
        var cell = new GridCell<GridPosition, System.Collections.Generic.List<int>>(pos, data);

        // Assert
        Assert.That(cell.Data, Is.SameAs(data));
        Assert.That(cell.Data.Count, Is.EqualTo(3));
    }

    [Test]
    public void GridCell_ShouldWorkWithValueTypes() {
        // Arrange & Act
        var cell = new GridCell<GridPosition, (int x, int y)>(new GridPosition(1, 2), (10, 20));

        // Assert
        Assert.That(cell.Data.x, Is.EqualTo(10));
        Assert.That(cell.Data.y, Is.EqualTo(20));
    }

    [Test]
    public void GridCell_ShouldPreservePosition() {
        // Arrange
        var pos = new GridPosition(5, 7);
        var cell = new GridCell<GridPosition, string>(pos, "test");

        // Act
        cell.Data = "modified";

        // Assert - Position should remain unchanged
        Assert.That(cell.Position, Is.EqualTo(pos));
    }
}
