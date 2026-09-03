namespace LSUtils.ProcessSystem;
using System;


/// <summary>Editable single-child inverter template; no runtime state.</summary>
public class LSProcessNodeInverter : ILSProcessLayerNode {
    public const string ClassName = nameof(LSProcessNodeInverter);
    private ILSProcessNode? _child;
    public string NodeID { get; }
    public int Order { get; internal set; }
    public LSProcessPriority Priority { get; internal set; }
    public NodeUpdatePolicy UpdatePolicy { get; }
    public LSProcessNodeCondition?[] Conditions { get; internal set; }
    public bool ReadOnly => UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES);

    internal LSProcessNodeInverter(string nodeID, LSProcessPriority priority = LSProcessPriority.NORMAL,
        int order = 0, NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.NONE,
        params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeID;
        Priority = priority;
        Order = order;
        UpdatePolicy = updatePolicy & (NodeUpdatePolicy.IGNORE_CHANGES | NodeUpdatePolicy.IGNORE_BUILDER);
        Conditions = conditions;
    }
    public void AddChild(ILSProcessNode child) {
        ArgumentNullException.ThrowIfNull(child);
        // Keep the existing builder rule: another child does not replace the first.
        _child ??= child;
    }
    public void AddChildren(params ILSProcessNode[] children) {
        if (children == null || children.Length == 0) throw new ArgumentNullException(nameof(children));
        if (children.Length > 1) throw new LSException("Inverters can only have one child.");
        AddChild(children[0]);
    }
    public bool HasChild(string nodeID) => _child?.NodeID == nodeID;
    public ILSProcessNode? GetChild(string nodeID) => HasChild(nodeID) ? _child : null;
    public ILSProcessNode[] GetChildren() => _child == null ? Array.Empty<ILSProcessNode>() : new[] { _child };
    public bool RemoveChild(string nodeID) {
        if (!HasChild(nodeID)) return false;
        _child = null;
        return true;
    }
    public void Reorder(int order) {
        Order = order;
        _child?.Reorder(0);
    }
    public ILSProcessLayerNode Clone() {
        var clone = new LSProcessNodeInverter(NodeID, Priority, Order, UpdatePolicy,
            (LSProcessNodeCondition?[])Conditions.Clone());
        if (_child != null) clone.AddChild(_child.Clone());
        return clone;
    }
    ILSProcessNode ILSProcessNode.Clone() => Clone();
}
