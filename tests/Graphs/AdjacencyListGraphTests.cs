namespace LSUtils.Tests.Graphs;

using System.Linq;
using NUnit.Framework;
using LSUtils.Graphs;

[TestFixture]
public class AdjacencyListGraphTests {
    [Test]
    public void AddNode_ShouldAddNodeToGraph() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();

        // Act
        graph.AddNode("A");

        // Assert
        Assert.That(graph.HasNode("A"), Is.True);
        Assert.That(graph.Nodes.Count(), Is.EqualTo(1));
    }

    [Test]
    public void AddNode_ShouldNotDuplicateNodes() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();

        // Act
        graph.AddNode("A");
        graph.AddNode("A");

        // Assert
        Assert.That(graph.Nodes.Count(), Is.EqualTo(1));
    }

    [Test]
    public void AddEdge_ShouldAddNodesAndEdge() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();

        // Act
        graph.AddEdge("A", "B");

        // Assert
        Assert.That(graph.HasNode("A"), Is.True);
        Assert.That(graph.HasNode("B"), Is.True);
        Assert.That(graph.GetNeighbors("A"), Does.Contain("B"));
    }

    [Test]
    public void AddEdge_ShouldBeDirectional() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();

        // Act
        graph.AddEdge("A", "B");

        // Assert
        Assert.That(graph.GetNeighbors("A"), Does.Contain("B"));
        Assert.That(graph.GetNeighbors("B"), Does.Not.Contain("A"));
    }

    [Test]
    public void GetNeighbors_ShouldReturnEmptyForNonExistentNode() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();

        // Act
        var neighbors = graph.GetNeighbors("NonExistent");

        // Assert
        Assert.That(neighbors, Is.Empty);
    }

    [Test]
    public void GetNeighbors_ShouldReturnAllNeighbors() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();
        graph.AddEdge("A", "B");
        graph.AddEdge("A", "C");
        graph.AddEdge("A", "D");

        // Act
        var neighbors = graph.GetNeighbors("A").ToList();

        // Assert
        Assert.That(neighbors, Has.Count.EqualTo(3));
        Assert.That(neighbors, Does.Contain("B"));
        Assert.That(neighbors, Does.Contain("C"));
        Assert.That(neighbors, Does.Contain("D"));
    }

    [Test]
    public void RemoveNode_ShouldRemoveNodeAndRelatedEdges() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();
        graph.AddEdge("A", "B");
        graph.AddEdge("B", "C");
        graph.AddEdge("C", "A");

        // Act
        var removed = graph.RemoveNode("B");

        // Assert
        Assert.That(removed, Is.True);
        Assert.That(graph.HasNode("B"), Is.False);
        Assert.That(graph.GetNeighbors("A"), Does.Not.Contain("B"));
    }

    [Test]
    public void RemoveNode_ShouldReturnFalseForNonExistentNode() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();

        // Act
        var removed = graph.RemoveNode("NonExistent");

        // Assert
        Assert.That(removed, Is.False);
    }

    [Test]
    public void RemoveEdge_ShouldRemoveSpecificEdge() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();
        graph.AddEdge("A", "B");
        graph.AddEdge("A", "C");

        // Act
        var removed = graph.RemoveEdge("A", "B");

        // Assert
        Assert.That(removed, Is.True);
        Assert.That(graph.GetNeighbors("A"), Does.Not.Contain("B"));
        Assert.That(graph.GetNeighbors("A"), Does.Contain("C"));
    }

    [Test]
    public void RemoveEdge_ShouldReturnFalseForNonExistentEdge() {
        // Arrange
        var graph = new AdjacencyListGraph<string>();
        graph.AddNode("A");

        // Act
        var removed = graph.RemoveEdge("A", "B");

        // Assert
        Assert.That(removed, Is.False);
    }

    [Test]
    public void Graph_ShouldWorkWithIntNodes() {
        // Arrange
        var graph = new AdjacencyListGraph<int>();

        // Act
        graph.AddEdge(1, 2);
        graph.AddEdge(2, 3);
        graph.AddEdge(1, 3);

        // Assert
        Assert.That(graph.Nodes.Count(), Is.EqualTo(3));
        Assert.That(graph.GetNeighbors(1).Count(), Is.EqualTo(2));
    }

    [Test]
    public void Graph_ShouldHandleComplexTopology() {
        // Arrange - Create a directed graph:
        //   A -> B -> D
        //   |    |
        //   v    v
        //   C -> E
        var graph = new AdjacencyListGraph<string>();

        // Act
        graph.AddEdge("A", "B");
        graph.AddEdge("A", "C");
        graph.AddEdge("B", "D");
        graph.AddEdge("B", "E");
        graph.AddEdge("C", "E");

        // Assert
        Assert.That(graph.Nodes.Count(), Is.EqualTo(5));
        Assert.That(graph.GetNeighbors("A").Count(), Is.EqualTo(2));
        Assert.That(graph.GetNeighbors("B").Count(), Is.EqualTo(2));
        Assert.That(graph.GetNeighbors("C").Count(), Is.EqualTo(1));
        Assert.That(graph.GetNeighbors("D"), Is.Empty);
        Assert.That(graph.GetNeighbors("E"), Is.Empty);
    }
}
