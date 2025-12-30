namespace LSUtils.Tests.Graphs;

using System.Linq;
using NUnit.Framework;
using LSUtils.Graphs;
using LSUtils.Grids;

[TestFixture]
public class GridGraphAdapterTests {
    [Test]
    public void GridGraphAdapter_ShouldAdaptDenseGrid() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        grid.SetCell(new GridPosition(1, 1), 42);

        // Act
        var graphAdapter = new GridGraphAdapter<GridPosition, int>(grid);

        // Assert
        Assert.That(graphAdapter.Nodes.Count(), Is.EqualTo(9));
    }

    [Test]
    public void GridGraphAdapter_ShouldAdaptSparseGrid() {
        // Arrange
        var grid = new SparseGrid<string>(5, 5);
        grid.SetCell(new GridPosition(2, 2), "center");

        // Act
        var graphAdapter = new GridGraphAdapter<GridPosition, string>(grid);

        // Assert
        Assert.That(graphAdapter.Nodes.Count(), Is.EqualTo(25));
    }

    [Test]
    public void GetNeighbors_ShouldReturn4CardinalNeighbors() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        var graphAdapter = new GridGraphAdapter<GridPosition, int>(grid, includeDiagonals: false);
        var center = new GridPosition(1, 1);

        // Act
        var neighbors = graphAdapter.GetNeighbors(center).ToList();

        // Assert
        Assert.That(neighbors.Count, Is.EqualTo(4));
        Assert.That(neighbors, Does.Contain(new GridPosition(1, 0))); // South
        Assert.That(neighbors, Does.Contain(new GridPosition(1, 2))); // North
        Assert.That(neighbors, Does.Contain(new GridPosition(0, 1))); // West
        Assert.That(neighbors, Does.Contain(new GridPosition(2, 1))); // East
    }

    [Test]
    public void GetNeighbors_ShouldReturn8NeighborsWithDiagonals() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        var graphAdapter = new GridGraphAdapter<GridPosition, int>(grid, includeDiagonals: true);
        var center = new GridPosition(1, 1);

        // Act
        var neighbors = graphAdapter.GetNeighbors(center).ToList();

        // Assert
        Assert.That(neighbors.Count, Is.EqualTo(8));
    }

    [Test]
    public void GetNeighbors_ShouldHandleEdgeCells() {
        // Arrange
        var grid = new DenseGrid<int>(3, 3);
        var graphAdapter = new GridGraphAdapter<GridPosition, int>(grid, includeDiagonals: false);
        var corner = new GridPosition(0, 0);

        // Act
        var neighbors = graphAdapter.GetNeighbors(corner).ToList();

        // Assert
        Assert.That(neighbors.Count, Is.EqualTo(2)); // Only North and East
    }

    [Test]
    public void HasNode_ShouldReturnTrueForValidPositions() {
        // Arrange
        var grid = new DenseGrid<int>(5, 5);
        var graphAdapter = new GridGraphAdapter<GridPosition, int>(grid);

        // Act & Assert
        Assert.That(graphAdapter.HasNode(new GridPosition(0, 0)), Is.True);
        Assert.That(graphAdapter.HasNode(new GridPosition(4, 4)), Is.True);
        Assert.That(graphAdapter.HasNode(new GridPosition(2, 2)), Is.True);
    }

    [Test]
    public void HasNode_ShouldReturnFalseForInvalidPositions() {
        // Arrange
        var grid = new DenseGrid<int>(5, 5);
        var graphAdapter = new GridGraphAdapter<GridPosition, int>(grid);

        // Act & Assert
        Assert.That(graphAdapter.HasNode(new GridPosition(-1, 0)), Is.False);
        Assert.That(graphAdapter.HasNode(new GridPosition(0, -1)), Is.False);
        Assert.That(graphAdapter.HasNode(new GridPosition(5, 5)), Is.False);
        Assert.That(graphAdapter.HasNode(new GridPosition(10, 10)), Is.False);
    }

    [Test]
    public void Nodes_ShouldReturnAllGridPositions() {
        // Arrange
        var grid = new DenseGrid<int>(2, 3);
        var graphAdapter = new GridGraphAdapter<GridPosition, int>(grid);

        // Act
        var nodes = graphAdapter.Nodes.ToList();

        // Assert
        Assert.That(nodes.Count, Is.EqualTo(6)); // 2x3 = 6 cells
        Assert.That(nodes, Does.Contain(new GridPosition(0, 0)));
        Assert.That(nodes, Does.Contain(new GridPosition(1, 2)));
    }

    [Test]
    public void GridGraphAdapter_ShouldWorkForPathfindingScenario() {
        // Arrange - Create a 5x5 grid
        var grid = new DenseGrid<bool>(5, 5);
        
        // Mark some cells as obstacles (false = walkable, true = blocked)
        grid.SetCell(new GridPosition(2, 1), true);
        grid.SetCell(new GridPosition(2, 2), true);
        grid.SetCell(new GridPosition(2, 3), true);

        var graphAdapter = new GridGraphAdapter<GridPosition, bool>(grid, includeDiagonals: false);

        // Act - Get neighbors of a position next to obstacles
        var position = new GridPosition(1, 2);
        var neighbors = graphAdapter.GetNeighbors(position).ToList();

        // Assert - Should have 4 neighbors (walls don't affect neighbor calculation)
        Assert.That(neighbors.Count, Is.EqualTo(4));
        Assert.That(neighbors, Does.Contain(new GridPosition(2, 2))); // Obstacle is still a neighbor
    }

    [Test]
    public void GridGraphAdapter_ShouldWorkWithDifferentGridTypes() {
        // Arrange
        var denseGrid = new DenseGrid<int>(3, 3);
        var sparseGrid = new SparseGrid<int>(3, 3);

        // Act
        var denseAdapter = new GridGraphAdapter<GridPosition, int>(denseGrid);
        var sparseAdapter = new GridGraphAdapter<GridPosition, int>(sparseGrid);

        // Assert - Both should behave identically
        Assert.That(denseAdapter.Nodes.Count(), Is.EqualTo(sparseAdapter.Nodes.Count()));
        var center = new GridPosition(1, 1);
        Assert.That(
            denseAdapter.GetNeighbors(center).Count(),
            Is.EqualTo(sparseAdapter.GetNeighbors(center).Count())
        );
    }
}
