namespace LSUtils.ProcessSystem;

/// <summary>Tree editing operations used by builders and manager templates only.</summary>
public interface ILSProcessLayerNode : ILSProcessNode {
    void AddChild(ILSProcessNode child);
    void AddChildren(params ILSProcessNode[] children);
    bool RemoveChild(string nodeID);
    bool HasChild(string nodeID);
    ILSProcessNode? GetChild(string nodeID);
    ILSProcessNode[] GetChildren();
    new ILSProcessLayerNode Clone();
}
