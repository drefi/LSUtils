namespace LSUtils.EventSystem;

public interface ILSEventLayerNode : ILSEventNode {
    void AddChild(ILSEventNode child);
    ILSEventNode? GetChild(string nodeID);
    bool HasChild(string nodeID);
    ILSEventNode[] GetChildren(); // Exposes children for navigation and cloning
    bool RemoveChild(string nodeID);
    new ILSEventLayerNode Clone();
}
