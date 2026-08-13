namespace LSUtils.Graphs;

using System.Collections.Generic;
using System.Linq;

public class UndirectedGraph<TNode> : IGraph<TNode> where TNode : notnull {
    private readonly Dictionary<TNode, HashSet<TNode>> _adjacency = new();

    public IEnumerable<TNode> Nodes => _adjacency.Keys;

    public IEnumerable<TNode> GetNeighbors(TNode node) {
        return _adjacency.TryGetValue(node, out var neighbors)
            ? neighbors
            : Enumerable.Empty<TNode>();
    }

    public bool HasNode(TNode node) {
        return _adjacency.ContainsKey(node);
    }

    public void AddNode(TNode node) {
        if (!_adjacency.ContainsKey(node)) _adjacency[node] = new HashSet<TNode>();
    }

    public void AddEdge(TNode a, TNode b) {
        AddNode(a);
        AddNode(b);
        _adjacency[a].Add(b);
        _adjacency[b].Add(a);
    }

    public bool RemoveNode(TNode node) {
        if (!_adjacency.Remove(node)) return false;

        foreach (var neighbors in _adjacency.Values) {
            neighbors.Remove(node);
        }

        return true;
    }

    public bool RemoveEdge(TNode a, TNode b) {
        bool removedA = _adjacency.TryGetValue(a, out var neighborsA) && neighborsA.Remove(b);
        bool removedB = _adjacency.TryGetValue(b, out var neighborsB) && neighborsB.Remove(a);
        return removedA || removedB;
    }
}
