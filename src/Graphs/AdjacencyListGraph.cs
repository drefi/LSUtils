using System.Collections.Generic;

namespace LSUtils.Graphs;

public class AdjacencyListGraph<TNode> : IGraph<TNode> where TNode : notnull {
    private readonly Dictionary<TNode, List<TNode>> _adjacency = new();
    
    public IEnumerable<TNode> Nodes => _adjacency.Keys;
    public IEnumerable<TNode> GetNeighbors(TNode node) => _adjacency.TryGetValue(node, out var n) ? n : System.Linq.Enumerable.Empty<TNode>();
    public bool HasNode(TNode node) => _adjacency.ContainsKey(node);

    /// <summary>
    /// Adiciona um nó ao grafo.
    /// </summary>
    public void AddNode(TNode node) {
        if (!_adjacency.ContainsKey(node)) {
            _adjacency[node] = new List<TNode>();
        }
    }

    /// <summary>
    /// Adiciona uma aresta direcionada do nó 'from' para o nó 'to'.
    /// </summary>
    public void AddEdge(TNode from, TNode to) {
        AddNode(from);
        AddNode(to);
        if (!_adjacency[from].Contains(to)) {
            _adjacency[from].Add(to);
        }
    }

    /// <summary>
    /// Remove um nó e todas as arestas relacionadas.
    /// </summary>
    public bool RemoveNode(TNode node) {
        if (!_adjacency.Remove(node)) return false;
        foreach (var neighbors in _adjacency.Values) {
            neighbors.Remove(node);
        }
        return true;
    }

    /// <summary>
    /// Remove uma aresta direcionada.
    /// </summary>
    public bool RemoveEdge(TNode from, TNode to) {
        return _adjacency.TryGetValue(from, out var neighbors) && neighbors.Remove(to);
    }
}
