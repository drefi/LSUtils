namespace LSUtils;

public abstract class ListenerEntry {
    /// <summary>
    /// The identifier of the listener.
    /// </summary>
    public System.Guid ListenerID { get; }
    /// <summary>
    /// The number of times the event can been triggered.
    /// </summary>
    /// <value>The number of triggers.</value>
    public int TotalTriggers { get; }
    /// <summary>
    /// Gets or sets the count of remaining triggers for the event.
    /// </summary>
    public int TriggersRemaining { get; protected set; }

    /// <summary>
    /// Indicates whether the listener is valid.
    /// </summary>
    public bool IsValid { get; }
    internal ListenerEntry(System.Guid listenerID, int triggers = -1) {
        ListenerID = listenerID;
        TriggersRemaining = TotalTriggers = triggers;
        IsValid = true;
    }

    public abstract int Execute(LSEvent @event, LSMessageHandler? onFailure = null);
}
internal class ListenerEntry<TEvent> : ListenerEntry where TEvent : LSEvent {
    protected LSListener<TEvent> _callback;
    internal ListenerEntry(System.Guid listenerID, LSListener<TEvent> callback, int triggers = -1) : base(listenerID, triggers) {
        _callback = callback;
    }
    public override int Execute(LSEvent @event, LSMessageHandler? onFailure = null) {
        if (!IsValid) {
            onFailure?.Invoke("listener_info_not_valid");
            return 0;
        }
        if (TriggersRemaining == 0) return 0;
        if (@event is TEvent typedEvent) {
            _callback(ListenerID, typedEvent);
        } else {
            onFailure?.Invoke("event_type_mismatch");
            return 0;
        }
        if (TotalTriggers == -1) return -1;
        TriggersRemaining--;
        return TriggersRemaining;
    }
}
