namespace LSUtils.Tests.Graphs;

using System.Linq;
using NUnit.Framework;
using LSUtils.Graphs;
using LSUtils.Graphs.Algorithms;

[TestFixture]
public class GraphAlgorithmsTests {
    [Test]
    public void BreadthFirstSearch_VisitsReachableNodes() {
        var graph = new UndirectedGraph<string>();
        graph.AddEdge("A", "B");
        graph.AddEdge("A", "C");
        graph.AddEdge("B", "D");

        var result = GraphAlgorithms.BreadthFirstSearch(graph, "A");

        Assert.That(result, Is.EquivalentTo(new[] { "A", "B", "C", "D" }));
        Assert.That(result.First(), Is.EqualTo("A"));
    }

    [Test]
    public void BreadthFirstSearch_MissingStart_ReturnsEmpty() {
        var graph = new UndirectedGraph<string>();

        var result = GraphAlgorithms.BreadthFirstSearch(graph, "missing");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void FloodFill_WithPredicate_StopsAtRejectedNodes() {
        var graph = new UndirectedGraph<string>();
        graph.AddEdge("grass-1", "grass-2");
        graph.AddEdge("grass-2", "water-1");
        graph.AddEdge("water-1", "grass-3");

        var result = GraphAlgorithms.FloodFill(graph, "grass-1", node => node.StartsWith("grass"));

        Assert.That(result, Is.EquivalentTo(new[] { "grass-1", "grass-2" }));
    }

    [Test]
    public void ConnectedComponents_ReturnsSeparatedGroups() {
        var graph = new UndirectedGraph<string>();
        graph.AddEdge("A", "B");
        graph.AddEdge("C", "D");
        graph.AddNode("E");

        var components = GraphAlgorithms.ConnectedComponents(graph);

        Assert.That(components, Has.Count.EqualTo(3));
        Assert.That(components.Any(c => c.ToHashSet().SetEquals(new[] { "A", "B" })), Is.True);
        Assert.That(components.Any(c => c.ToHashSet().SetEquals(new[] { "C", "D" })), Is.True);
        Assert.That(components.Any(c => c.ToHashSet().SetEquals(new[] { "E" })), Is.True);
    }

    [Test]
    public void AStar_ReturnsShortestPath() {
        var graph = new UndirectedGraph<int>();
        graph.AddEdge(0, 1);
        graph.AddEdge(1, 2);
        graph.AddEdge(0, 3);
        graph.AddEdge(3, 2);

        var path = GraphAlgorithms.AStar(
            graph,
            0,
            2,
            (from, to) => 0f,
            (from, to) => from == 3 || to == 3 ? 10f : 1f);

        Assert.That(path, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void AStar_WhenNoPath_ReturnsEmpty() {
        var graph = new UndirectedGraph<string>();
        graph.AddEdge("A", "B");
        graph.AddNode("C");

        var path = GraphAlgorithms.AStar(graph, "A", "C", (_, _) => 0f, (_, _) => 1f);

        Assert.That(path, Is.Empty);
    }

    [Test]
    public void AStar_WithNegativeCost_Throws() {
        var graph = new UndirectedGraph<string>();
        graph.AddEdge("A", "B");

        Assert.Throws<LSArgumentException>(() =>
            GraphAlgorithms.AStar(graph, "A", "B", (_, _) => 0f, (_, _) => -1f));
    }
}
