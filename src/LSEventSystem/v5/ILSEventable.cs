namespace LSUtils.EventSystem;

public interface ILSEventable {
    LSEventProcessStatus Initialize(LSEventContextManager manager, ILSEventLayerNode context);
}
