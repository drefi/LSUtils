namespace LSUtils.EventSystem;

public interface ILSEventLayerNode : ILSEventNode {
    void AddChild(ILSEventNode child);
    ILSEventNode? FindChild(string label);
    bool HasChild(string label);
    ILSEventNode[] GetChildren(); // Exposes children for navigation and cloning
    bool RemoveChild(string label);
    new ILSEventLayerNode Clone();
}
