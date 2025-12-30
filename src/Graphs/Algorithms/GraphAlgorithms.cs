namespace LSUtils.Graphs.Algorithms;

using System.Collections.Generic;
public delegate float NodeDistanceFunc<TNode>(TNode from, TNode to);
public static class GraphAlgorithms {
    public static List<TNode> AStar<TNode>(
        IGraph<TNode> graph,
        TNode start,
        TNode goal,
        NodeDistanceFunc<TNode> heuristic,
        NodeDistanceFunc<TNode> cost) where TNode : notnull {
        // Implementação padrão do A*...
        throw new LSNotImplementedException();
    }
}
