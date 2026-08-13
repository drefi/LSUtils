namespace LSUtils.Graphs;

using System.Collections.Generic;
using System.Linq;

public class RelationGraph<TNode, TRelation> : IRelationGraph<TNode, TRelation> where TNode : notnull {
    private readonly Dictionary<TNode, List<GraphRelation<TNode, TRelation>>> _relations = new();

    public IEnumerable<TNode> Nodes => _relations.Keys;

    public IEnumerable<TNode> GetNeighbors(TNode node) {
        return _relations.TryGetValue(node, out var relations)
            ? relations.Select(r => r.To).Distinct()
            : Enumerable.Empty<TNode>();
    }

    public IEnumerable<GraphRelation<TNode, TRelation>> GetRelations(TNode node) {
        return _relations.TryGetValue(node, out var relations)
            ? relations
            : Enumerable.Empty<GraphRelation<TNode, TRelation>>();
    }

    public IEnumerable<GraphRelation<TNode, TRelation>> GetRelations(TNode from, TNode to) {
        return GetRelations(from).Where(r => EqualityComparer<TNode>.Default.Equals(r.To, to));
    }

    public bool HasNode(TNode node) {
        return _relations.ContainsKey(node);
    }

    public void AddNode(TNode node) {
        if (!_relations.ContainsKey(node)) _relations[node] = new List<GraphRelation<TNode, TRelation>>();
    }

    public void AddRelation(TNode from, TNode to, TRelation relation, float weight = 1f) {
        AddNode(from);
        AddNode(to);

        var edge = new GraphRelation<TNode, TRelation>(from, to, relation, weight);
        if (!_relations[from].Contains(edge)) _relations[from].Add(edge);
    }

    public void AddUndirectedRelation(TNode a, TNode b, TRelation relation, float weight = 1f) {
        AddRelation(a, b, relation, weight);
        AddRelation(b, a, relation, weight);
    }

    public bool RemoveNode(TNode node) {
        if (!_relations.Remove(node)) return false;

        foreach (var relations in _relations.Values) {
            relations.RemoveAll(r => EqualityComparer<TNode>.Default.Equals(r.To, node));
        }

        return true;
    }

    public bool RemoveRelations(TNode from, TNode to) {
        return _relations.TryGetValue(from, out var relations) &&
               relations.RemoveAll(r => EqualityComparer<TNode>.Default.Equals(r.To, to)) > 0;
    }
}
