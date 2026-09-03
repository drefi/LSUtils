namespace LSUtils.ProcessSystem;

/// <summary>Editable composition metadata. Not an executable node.</summary>
public interface ILSProcessNode {
    string NodeID { get; }
    LSProcessPriority Priority { get; }
    LSProcessNodeCondition?[] Conditions { get; }
    int Order { get; }
    NodeUpdatePolicy UpdatePolicy { get; }
    ILSProcessNode Clone();
    void Reorder(int order);
}
