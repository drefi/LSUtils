namespace LSUtils.EventSystem;

public interface ILSEventable {
    public string InstanceID { get; }
    LSEventProcessStatus Initialize(LSEventContextManager manager, ILSEventLayerNode context);
}
