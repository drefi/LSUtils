namespace LSUtils.EventSystem;

public interface ILSEventable {
    public System.Guid ID { get; }
    LSEventProcessStatus Initialize(LSEventContextManager manager, ILSEventLayerNode context);
}
