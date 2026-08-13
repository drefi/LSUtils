namespace LSUtils.Tests.Graphs;

using System.Linq;
using NUnit.Framework;
using LSUtils.Graphs;

[TestFixture]
public class UndirectedGraphTests {
    [Test]
    public void AddEdge_ShouldConnectBothDirections() {
        var graph = new UndirectedGraph<string>();

        graph.AddEdge("A", "B");

        Assert.That(graph.GetNeighbors("A"), Does.Contain("B"));
        Assert.That(graph.GetNeighbors("B"), Does.Contain("A"));
    }

    [Test]
    public void AddEdge_ShouldNotDuplicateNeighbors() {
        var graph = new UndirectedGraph<string>();

        graph.AddEdge("A", "B");
        graph.AddEdge("A", "B");

        Assert.That(graph.GetNeighbors("A").Count(), Is.EqualTo(1));
        Assert.That(graph.GetNeighbors("B").Count(), Is.EqualTo(1));
    }

    [Test]
    public void RemoveEdge_ShouldRemoveBothDirections() {
        var graph = new UndirectedGraph<string>();
        graph.AddEdge("A", "B");

        graph.RemoveEdge("A", "B");

        Assert.That(graph.GetNeighbors("A"), Does.Not.Contain("B"));
        Assert.That(graph.GetNeighbors("B"), Does.Not.Contain("A"));
    }
}
