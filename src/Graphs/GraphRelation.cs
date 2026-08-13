namespace LSUtils.Graphs;

public readonly record struct GraphRelation<TNode, TRelation>(
    TNode From,
    TNode To,
    TRelation Relation,
    float Weight = 1f
) where TNode : notnull;
