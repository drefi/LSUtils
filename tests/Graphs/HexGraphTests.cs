using System.Linq;
using NUnit.Framework;
using LSUtils.Graphs;

namespace LSUtils.Tests.Graphs;

[TestFixture]
public class HexGraphTests
{
    [Test]
    public void GetNeighbours_ShouldReturnAdjacentCells()
    {
        var graph = new HexGraph<int>(rowCount: 2, nominalWidth: 3);
        // Row 0
        graph[0, 0] = 10;
        graph[0, 1] = 11;
        graph[0, 2] = 12;
        // Row 1 (rowWidth = nominalWidth - 1)
        graph[1, 0] = 13;
        graph[1, 1] = 14;

        var neighbours = graph.GetNeighbours(1).ToArray(); // index 1 is row 0, middle cell (value 11)

        Assert.That(neighbours.Length, Is.EqualTo(4));
        Assert.That(neighbours, Does.Contain(10));
        Assert.That(neighbours, Does.Contain(12));
        Assert.That(neighbours, Does.Contain(13));
        Assert.That(neighbours, Does.Contain(14));
    }
}
