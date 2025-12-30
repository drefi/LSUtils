namespace LSUtils.Tests.Grids;

using System.Linq;
using NUnit.Framework;
using LSUtils.Grids;

[TestFixture]
public class DenseGridTests {
    [Test]
    public void DenseGrid_ShouldInitializeWithCorrectDimensions() {
        // Arrange & Act
        var grid = new DenseGrid<int>(5, 3);

        // Assert
        Assert.That(grid.Width, Is.EqualTo(5));
        Assert.That(grid.Height, Is.EqualTo(3));
    }

    [Test]
    public void SetCell_ShouldStoreValue() {
        // Arrange
        var grid = new DenseGrid<string>(3, 3);
        var pos = new GridPosition(1, 1);

        // Act
        var result = grid.SetCell(pos, "test");

        // Assert
        Assert.That(result, Is.True);
        Assert.That(grid.GetCell(pos), Is.EqualTo("test"));
    }

    [Test]
    public void GetCell_ShouldReturnDefaultForUnsetCells() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);

        // Act
        var value = grid.GetCell(new GridPosition(1, 1));

        // Assert
        Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void SetCell_ShouldReturnFalseForInvalidPosition() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);

        // Act
        var result = grid.SetCell(new GridPosition(5, 5), 42);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void GetCell_ShouldReturnDefaultForInvalidPosition() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);

        // Act
        var value = grid.GetCell(new GridPosition(-1, 0));

        // Assert
        Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void IsValidPosition_ShouldReturnTrueForValidPositions() {
        // Arrange
        var grid = new DenseGrid<int>(5, 5);

        // Assert
        Assert.That(grid.IsValidPosition(new GridPosition(0, 0)), Is.True);
        Assert.That(grid.IsValidPosition(new GridPosition(4, 4)), Is.True);
        Assert.That(grid.IsValidPosition(new GridPosition(2, 3)), Is.True);
    }

    [Test]
    public void IsValidPosition_ShouldReturnFalseForInvalidPositions() {
        // Arrange
        var grid = new DenseGrid<int>(5, 5);

        // Assert
        Assert.That(grid.IsValidPosition(new GridPosition(-1, 0)), Is.False);
        Assert.That(grid.IsValidPosition(new GridPosition(0, -1)), Is.False);
        Assert.That(grid.IsValidPosition(new GridPosition(5, 5)), Is.False);
        Assert.That(grid.IsValidPosition(new GridPosition(10, 10)), Is.False);
    }

    [Test]
    public void GetNeighbors_ShouldReturn4CardinalNeighbors() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        var center = new GridPosition(1, 1);

        // Act
        var neighbors = grid.GetNeighbors(center, includeDiagonals: false).ToList();

        // Assert
        Assert.That(neighbors.Count, Is.EqualTo(4));
        Assert.That(neighbors, Does.Contain(new GridPosition(1, 0)));
        Assert.That(neighbors, Does.Contain(new GridPosition(1, 2)));
        Assert.That(neighbors, Does.Contain(new GridPosition(0, 1)));
        Assert.That(neighbors, Does.Contain(new GridPosition(2, 1)));
    }

    [Test]
    public void GetNeighbors_ShouldReturn8NeighborsWithDiagonals() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        var center = new GridPosition(1, 1);

        // Act
        var neighbors = grid.GetNeighbors(center, includeDiagonals: true).ToList();

        // Assert
        Assert.That(neighbors.Count, Is.EqualTo(8));
    }

    [Test]
    public void GetNeighbors_ShouldHandleEdgePositions() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        var corner = new GridPosition(0, 0);

        // Act
        var neighbors = grid.GetNeighbors(corner, includeDiagonals: false).ToList();

        // Assert
        Assert.That(neighbors.Count, Is.EqualTo(2));
    }

    [Test]
    public void GetAllPositions_ShouldReturnAllCells() {
        // Arrange
        var grid = new DenseGrid<int>(2, 3);

        // Act
        var positions = grid.GetAllPositions().ToList();

        // Assert
        Assert.That(positions.Count, Is.EqualTo(6));
        Assert.That(positions, Does.Contain(new GridPosition(0, 0)));
        Assert.That(positions, Does.Contain(new GridPosition(1, 2)));
    }

    [Test]
    public void Clear_ShouldResetAllCells() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        grid.SetCell(new GridPosition(0, 0), 10);
        grid.SetCell(new GridPosition(1, 1), 20);
        grid.SetCell(new GridPosition(2, 2), 30);

        // Act
        grid.Clear();

        // Assert
        Assert.That(grid.GetCell(new GridPosition(0, 0)), Is.EqualTo(0));
        Assert.That(grid.GetCell(new GridPosition(1, 1)), Is.EqualTo(0));
        Assert.That(grid.GetCell(new GridPosition(2, 2)), Is.EqualTo(0));
    }

    [Test]
    public void DenseGrid_ShouldWorkWithReferenceTypes() {
        // Arrange
        var grid = new DenseGrid<string>(2, 2);

        // Act
        grid.SetCell(new GridPosition(0, 0), "A");
        grid.SetCell(new GridPosition(1, 1), "B");

        // Assert
        Assert.That(grid.GetCell(new GridPosition(0, 0)), Is.EqualTo("A"));
        Assert.That(grid.GetCell(new GridPosition(1, 1)), Is.EqualTo("B"));
        Assert.That(grid.GetCell(new GridPosition(0, 1)), Is.Null);
    }

    [Test]
    public void DenseGrid_ShouldOverwriteExistingValues() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        var pos = new GridPosition(1, 1);

        // Act
        grid.SetCell(pos, 10);
        grid.SetCell(pos, 20);

        // Assert
        Assert.That(grid.GetCell(pos), Is.EqualTo(20));
    }
}
