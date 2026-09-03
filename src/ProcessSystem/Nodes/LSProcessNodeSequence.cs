namespace LSUtils.ProcessSystem;
using System.Collections.Generic;
using System.Linq;


/// <summary>Editable sequence template. Execution state belongs exclusively to the session.</summary>
public class LSProcessNodeSequence : ILSProcessLayerNode {
    public const string ClassName = nameof(LSProcessNodeSequence);
    private readonly Dictionary<string, ILSProcessNode> _children = new();
    public string NodeID { get; }
    public int Order { get; internal set; }
    public LSProcessPriority Priority { get; }
    public NodeUpdatePolicy UpdatePolicy { get; }
    public LSProcessNodeCondition?[] Conditions { get; }
    public bool ReadOnly => UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES);

    internal LSProcessNodeSequence(string nodeId, int order,
        LSProcessPriority priority = LSProcessPriority.NORMAL,
        NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.NONE, params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeId;
        Order = order;
        Priority = priority;
        UpdatePolicy = updatePolicy & (NodeUpdatePolicy.IGNORE_CHANGES | NodeUpdatePolicy.IGNORE_BUILDER);
        Conditions = conditions;
    }

    public void AddChild(ILSProcessNode child) => _children[child.NodeID] = child;
    public void AddChildren(params ILSProcessNode[] children) {
        foreach (var child in children) AddChild(child);
    }
    public bool RemoveChild(string nodeID) => _children.Remove(nodeID);
    public bool HasChild(string nodeID) => _children.ContainsKey(nodeID);
    public ILSProcessNode? GetChild(string nodeID) => _children.TryGetValue(nodeID, out var child) ? child : null;
    public ILSProcessNode[] GetChildren() => _children.Values.ToArray();
    public void Reorder(int order) {
        Order = order;
        var index = 0;
        foreach (var child in _children.Values) child.Reorder(index++);
    }
    public ILSProcessLayerNode Clone() {
        var clone = new LSProcessNodeSequence(NodeID, Order, Priority, UpdatePolicy,
            (LSProcessNodeCondition?[])Conditions.Clone());
        foreach (var child in _children.Values) clone.AddChild(child.Clone());
        return clone;
    }
    ILSProcessNode ILSProcessNode.Clone() => Clone();
}
