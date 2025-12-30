using System.Collections.Generic;
using NUnit.Framework;
using LSUtils.Graphs;

namespace LSUtils.Tests.Graphs;

[TestFixture]
public class GridGraphTests
{
    [Test]
    public void GetNeighbours_NoDiagonals_ShouldReturnCardinalsOnly()
    {
        var grid = new GridGraph<int>(width: 2, length: 2, includeDiagonals: false);
        grid[0, 0] = 1;
        grid[1, 0] = 2;
        grid[0, 1] = 3;
        grid[1, 1] = 4;

        var buffer = new List<int>();
        var count = grid.GetNeighbours(0, 0, buffer);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(buffer, Does.Contain(2)); // east
        Assert.That(buffer, Does.Contain(3)); // north
    }

    [Test]
    public void GetNeighbours_WithDiagonals_ShouldIncludeDiagonal()
    {
        var grid = new GridGraph<int>(width: 2, length: 2, includeDiagonals: true);
        grid[0, 0] = 1;
        grid[1, 0] = 2;
        grid[0, 1] = 3;
        grid[1, 1] = 4;

        var buffer = new List<int>();
        var count = grid.GetNeighbours(0, 0, buffer);

        Assert.That(count, Is.EqualTo(3));
        Assert.That(buffer, Does.Contain(4)); // NE
    }

    [Test]
    public void Indexer_OutOfRange_ShouldReturnDefault()
    {
        var grid = new GridGraph<int>(width: 2, length: 2, includeDiagonals: false);
        Assert.That(grid[-1, 0], Is.EqualTo(0));
        Assert.That(grid[2, 2], Is.EqualTo(0));
    }
}
