namespace LSUtils.ProcessSystem;

/// <summary>Editable handler template. Counts and results are held by execution nodes.</summary>
public class LSProcessNodeHandler : ILSProcessNode {
    public const string ClassName = nameof(LSProcessNodeHandler);
    public string NodeID { get; }
    public LSProcessHandler Handler { get; }
    public int Order { get; internal set; }
    public LSProcessPriority Priority { get; }
    public NodeUpdatePolicy UpdatePolicy { get; }
    public LSProcessNodeCondition?[] Conditions { get; }

    internal LSProcessNodeHandler(string nodeID, LSProcessHandler handler, int order,
        LSProcessPriority priority = LSProcessPriority.NORMAL,
        NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_HANDLER,
        params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeID;
        Handler = handler;
        Order = order;
        Priority = priority;
        UpdatePolicy = updatePolicy;
        Conditions = conditions;
    }
    public void Reorder(int order) => Order = order;
    public ILSProcessNode Clone() => new LSProcessNodeHandler(NodeID, Handler, Order, Priority,
        UpdatePolicy, (LSProcessNodeCondition?[])Conditions.Clone());
}
