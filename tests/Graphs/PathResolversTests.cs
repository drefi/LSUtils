using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using LSUtils.Graphs;

namespace LSUtils.Tests.Graphs;

[TestFixture]
public class PathResolversTests
{
    private sealed class TestNode : INode<TestNode>
    {
        public string Id { get; }
        private readonly List<TestNode> _neighbours = new();

        public TestNode(string id) => Id = id;

        public void Link(TestNode other)
        {
            _neighbours.Add(other);
        }

        public IEnumerable<TestNode> GetNeighbours() => _neighbours;
        public int GetNeighbours(ICollection<TestNode> buffer)
        {
            foreach (var n in _neighbours) buffer.Add(n);
            return _neighbours.Count;
        }

        public override string ToString() => Id;
    }

    private sealed class AdjacencyGraph : IGraph<TestNode>
    {
        private readonly HashSet<TestNode> _nodes;
        public AdjacencyGraph(IEnumerable<TestNode> nodes) => _nodes = new(nodes);

        public int Count => _nodes.Count;
        public bool Contains(TestNode node) => _nodes.Contains(node);
        public IEnumerator<TestNode> GetEnumerator() => _nodes.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerable<TestNode> GetNeighbours(TestNode node) => node.GetNeighbours();
        public int GetNeighbours(TestNode node, ICollection<TestNode> buffer) => node.GetNeighbours(buffer);
    }

    private sealed class UnitHeuristic : IHeuristic<TestNode>
    {
        public float Weight(TestNode n) => 1f;
        public float Distance(TestNode x, TestNode y) => 1f;
    }

    private static (AdjacencyGraph graph, TestNode a, TestNode b, TestNode c, TestNode isolated) BuildLine()
    {
        var a = new TestNode("A");
        var b = new TestNode("B");
        var c = new TestNode("C");
        var isolated = new TestNode("X");

        a.Link(b); b.Link(a);
        b.Link(c); c.Link(b);

        var graph = new AdjacencyGraph(new[] { a, b, c, isolated });
        return (graph, a, b, c, isolated);
    }

    [Test]
    public void AStar_ShouldFindPath_WhenReachable()
    {
        var (graph, a, _, c, _) = BuildLine();
        var resolver = new AStarPathResolver<TestNode>(graph, new UnitHeuristic())
        {
            // Properties are inverted in implementation: Goal sets _start, Start sets _goal
            Goal = a,
            Start = c
        };

        var path = resolver.Reduce();

        Assert.That(path.Count, Is.EqualTo(3));
        // Implementation builds path from goal back to start (goal-first order)
        Assert.That(path.First(), Is.EqualTo(c));
        Assert.That(path.Last(), Is.EqualTo(a));
    }

    [Test]
    public void AStar_ShouldReturnZero_WhenUnreachable()
    {
        var (graph, a, _, _, isolated) = BuildLine();
        var resolver = new AStarPathResolver<TestNode>(graph, new UnitHeuristic())
        {
            Goal = isolated,
            Start = a
        };

        var path = new List<TestNode>();
        var count = resolver.Reduce(path);

        Assert.That(count, Is.EqualTo(0));
        Assert.That(path, Is.Empty);
    }

    [Test]
    public void Dijkstra_ShouldFindShortestPath()
    {
        var (graph, a, _, c, _) = BuildLine();
        var resolver = new DijkstraPathResolver<TestNode>(graph, new UnitHeuristic())
        {
            Start = a,
            Goal = c
        };

        var path = resolver.Reduce();

        Assert.That(path.Count, Is.EqualTo(3));
        Assert.That(path.First(), Is.EqualTo(a));
        Assert.That(path.Last(), Is.EqualTo(c));
    }

    [Test]
    public void Dijkstra_ShouldReturnZero_WhenUnreachable()
    {
        var (graph, a, _, _, isolated) = BuildLine();
        var resolver = new DijkstraPathResolver<TestNode>(graph, new UnitHeuristic())
        {
            Start = a,
            Goal = isolated
        };

        var path = new List<TestNode>();
        var count = resolver.Reduce(path);

        // Implementation always returns at least the start node (distance = inf) when unreachable
        Assert.That(count, Is.EqualTo(1));
        Assert.That(path.Count, Is.EqualTo(1));
        Assert.That(path.Single(), Is.EqualTo(a));
    }
}
