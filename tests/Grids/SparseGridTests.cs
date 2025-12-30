namespace LSUtils.Tests.Grids;

using System.Linq;
using NUnit.Framework;
using LSUtils.Grids;

[TestFixture]
public class SparseGridTests {
    [Test]
    public void SparseGrid_ShouldInitializeWithCorrectDimensions() {
        // Arrange & Act
        var grid = new SparseGrid<int>(10, 8);

        // Assert
        Assert.That(grid.Width, Is.EqualTo(10));
        Assert.That(grid.Height, Is.EqualTo(8));
    }

    [Test]
    public void SetCell_ShouldStoreValue() {
        // Arrange
        var grid = new SparseGrid<string>(5, 5);
        var pos = new GridPosition(2, 3);

        // Act
        var result = grid.SetCell(pos, "value");

        // Assert
        Assert.That(result, Is.True);
        Assert.That(grid.GetCell(pos), Is.EqualTo("value"));
    }

    [Test]
    public void GetCell_ShouldReturnDefaultForUnsetCells() {
        // Arrange
        var grid = new SparseGrid<int>(5, 5);

        // Act
        var value = grid.GetCell(new GridPosition(2, 2));

        // Assert
        Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void SetCell_ShouldReturnFalseForInvalidPosition() {
        // Arrange
        var grid = new SparseGrid<int>(5, 5);

        // Act
        var result = grid.SetCell(new GridPosition(10, 10), 42);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void GetCell_ShouldReturnDefaultForInvalidPosition() {
        // Arrange
        var grid = new SparseGrid<int>(5, 5);

        // Act
        var value = grid.GetCell(new GridPosition(-1, -1));

        // Assert
        Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void IsValidPosition_ShouldWorkCorrectly() {
        // Arrange
        var grid = new SparseGrid<int>(5, 5);

        // Assert
        Assert.That(grid.IsValidPosition(new GridPosition(0, 0)), Is.True);
        Assert.That(grid.IsValidPosition(new GridPosition(4, 4)), Is.True);
        Assert.That(grid.IsValidPosition(new GridPosition(-1, 0)), Is.False);
        Assert.That(grid.IsValidPosition(new GridPosition(5, 5)), Is.False);
    }

    [Test]
    public void GetNeighbors_ShouldReturn4CardinalNeighbors() {
        // Arrange
        var grid = new SparseGrid<int>(3, 3);
        var center = new GridPosition(1, 1);

        // Act
        var neighbors = grid.GetNeighbors(center, includeDiagonals: false).ToList();

        // Assert
        Assert.That(neighbors.Count, Is.EqualTo(4));
    }

    [Test]
    public void GetNeighbors_ShouldReturn8NeighborsWithDiagonals() {
        // Arrange
        var grid = new SparseGrid<int>(3, 3);
        var center = new GridPosition(1, 1);

        // Act
        var neighbors = grid.GetNeighbors(center, includeDiagonals: true).ToList();

        // Assert
        Assert.That(neighbors.Count, Is.EqualTo(8));
    }

    [Test]
    public void GetAllPositions_ShouldReturnAllCells() {
        // Arrange
        var grid = new SparseGrid<int>(2, 3);

        // Act
        var positions = grid.GetAllPositions().ToList();

        // Assert
        Assert.That(positions.Count, Is.EqualTo(6));
    }

    [Test]
    public void Clear_ShouldRemoveAllStoredValues() {
        // Arrange
        var grid = new SparseGrid<int>(5, 5);
        grid.SetCell(new GridPosition(0, 0), 10);
        grid.SetCell(new GridPosition(2, 2), 20);
        grid.SetCell(new GridPosition(4, 4), 30);

        // Act
        grid.Clear();

        // Assert
        Assert.That(grid.GetCell(new GridPosition(0, 0)), Is.EqualTo(0));
        Assert.That(grid.GetCell(new GridPosition(2, 2)), Is.EqualTo(0));
        Assert.That(grid.GetCell(new GridPosition(4, 4)), Is.EqualTo(0));
    }

    [Test]
    public void SparseGrid_ShouldHandleLargeGridsEfficiently() {
        // Arrange & Act
        var grid = new SparseGrid<int>(10000, 10000);
        grid.SetCell(new GridPosition(5000, 5000), 42);

        // Assert
        Assert.That(grid.GetCell(new GridPosition(5000, 5000)), Is.EqualTo(42));
        Assert.That(grid.Width, Is.EqualTo(10000));
        Assert.That(grid.Height, Is.EqualTo(10000));
    }

    [Test]
    public void SparseGrid_ShouldOverwriteExistingValues() {
        // Arrange
        var grid = new SparseGrid<string>(3, 3);
        var pos = new GridPosition(1, 1);

        // Act
        grid.SetCell(pos, "first");
        grid.SetCell(pos, "second");

        // Assert
        Assert.That(grid.GetCell(pos), Is.EqualTo("second"));
    }

    [Test]
    public void SparseGrid_ShouldWorkWithComplexTypes() {
        // Arrange
        var grid = new SparseGrid<(int x, int y)>(3, 3);
        var pos = new GridPosition(1, 2);

        // Act
        grid.SetCell(pos, (10, 20));

        // Assert
        var value = grid.GetCell(pos);
        Assert.That(value.x, Is.EqualTo(10));
        Assert.That(value.y, Is.EqualTo(20));
    }
}
