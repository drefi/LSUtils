namespace LSUtils.Tests.Grids;

using NUnit.Framework;
using LSUtils.Grids;

[TestFixture]
public class GridPositionTests {
    [Test]
    public void GridPosition_ShouldStoreCoordinates() {
        // Arrange & Act
        var pos = new GridPosition(5, 7);

        // Assert
        Assert.That(pos.X, Is.EqualTo(5));
        Assert.That(pos.Y, Is.EqualTo(7));
    }

    [Test]
    public void GridPosition_ShouldSupportValueEquality() {
        // Arrange
        var pos1 = new GridPosition(3, 4);
        var pos2 = new GridPosition(3, 4);
        var pos3 = new GridPosition(4, 3);

        // Assert
        Assert.That(pos1, Is.EqualTo(pos2));
        Assert.That(pos1, Is.Not.EqualTo(pos3));
    }

    [Test]
    public void GridPosition_ShouldWorkAsStructWithValueSemantics() {
        // Arrange
        var pos1 = new GridPosition(1, 2);
        var pos2 = pos1;

        // Act
        pos2 = new GridPosition(3, 4);

        // Assert - pos1 should not be affected
        Assert.That(pos1.X, Is.EqualTo(1));
        Assert.That(pos1.Y, Is.EqualTo(2));
        Assert.That(pos2.X, Is.EqualTo(3));
        Assert.That(pos2.Y, Is.EqualTo(4));
    }

    [Test]
    public void GridPosition_ShouldWorkInHashCollections() {
        // Arrange
        var dict = new System.Collections.Generic.Dictionary<GridPosition, string>();
        var pos = new GridPosition(2, 3);

        // Act
        dict[pos] = "test";

        // Assert
        Assert.That(dict[new GridPosition(2, 3)], Is.EqualTo("test"));
    }

    [Test]
    public void GridPosition_ShouldHandleNegativeCoordinates() {
        // Arrange & Act
        var pos = new GridPosition(-5, -10);

        // Assert
        Assert.That(pos.X, Is.EqualTo(-5));
        Assert.That(pos.Y, Is.EqualTo(-10));
    }

    [Test]
    public void GridPosition_ShouldHandleZeroCoordinates() {
        // Arrange & Act
        var pos = new GridPosition(0, 0);

        // Assert
        Assert.That(pos.X, Is.EqualTo(0));
        Assert.That(pos.Y, Is.EqualTo(0));
    }
}
