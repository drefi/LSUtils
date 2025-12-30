namespace LSUtils.Graphs;

using System.Collections.Generic;
public interface IGraph<TNode> where TNode : notnull {
    IEnumerable<TNode> Nodes { get; }
    IEnumerable<TNode> GetNeighbors(TNode node);
    bool HasNode(TNode node);
}
