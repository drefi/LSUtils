namespace LSUtils.Graphs;

public record Edge<TNode>(TNode From, TNode To, float Cost = 1f) where TNode : notnull;
