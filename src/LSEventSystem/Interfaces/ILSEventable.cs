namespace LSUtils.EventSystem;

public interface ILSEventable {
    /// <summary>
    /// Gets the dispatcher associated with this eventable.
    /// </summary>
    LSDispatcher? Dispatcher { get; }
    EventProcessResult Initialize(LSEventOptions options);
}
