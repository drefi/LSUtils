namespace LSUtils;

// public class ListenerEntry {
//     protected LSListener _callback;
//     /// <summary>
//     /// The identifier of the listener.
//     /// </summary>
//     public System.Guid ListenerID { get; }
//     /// <summary>
//     /// The number of times the event can been triggered.
//     /// </summary>
//     /// <value>The number of triggers.</value>
//     public int TotalTriggers { get; }
//     /// <summary>
//     /// Gets or sets the count of remaining triggers for the event.
//     /// </summary>
//     public int TriggersRemaining { get; protected set; }

//     /// <summary>
//     /// Indicates whether the listener is valid.
//     /// </summary>
//     public bool IsValid { get; }
//     internal ListenerEntry(System.Guid listenerID, LSListener callback, int triggers = -1) {
//         _callback = callback;
//         ListenerID = listenerID;
//         TriggersRemaining = TotalTriggers = triggers;
//         IsValid = true;
//     }

//     public virtual int Execute(LSEvent @event) {
//         if (!IsValid) {
//             throw new LSException("listener_info_not_valid");
//         }
//         if (TriggersRemaining == 0) return 0;
//         _callback(ListenerID, @event);
//         if (TotalTriggers == -1) return -1;
//         TriggersRemaining--;
//         return TriggersRemaining;
//     }
// }
internal class ListenerEntry<TEvent> where TEvent : LSEvent {
    protected LSListener<TEvent> _callback;
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
    internal ListenerEntry(System.Guid listenerID, LSListener<TEvent> callback, int triggers = -1) {
        ListenerID = listenerID == default || listenerID == System.Guid.Empty ? System.Guid.NewGuid() : listenerID;
        TotalTriggers = triggers;
        TriggersRemaining = triggers;
        _callback = callback;
        IsValid = true;
    }
    public int Execute(LSEvent @event) {
        if (!IsValid) {
            throw new LSException("listener_info_not_valid");
        }
        if (TriggersRemaining == 0) return 0;
        if (@event is TEvent typedEvent) {
            _callback(ListenerID, typedEvent);
        } else {
            throw new LSException("event_type_mismatch");
        }
        if (TotalTriggers == -1) return -1;
        TriggersRemaining--;
        return TriggersRemaining;
    }
}
