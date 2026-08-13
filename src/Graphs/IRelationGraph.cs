namespace LSUtils.Graphs;

using System.Collections.Generic;

public interface IRelationGraph<TNode, TRelation> : IGraph<TNode> where TNode : notnull {
    IEnumerable<GraphRelation<TNode, TRelation>> GetRelations(TNode node);
    IEnumerable<GraphRelation<TNode, TRelation>> GetRelations(TNode from, TNode to);
}
