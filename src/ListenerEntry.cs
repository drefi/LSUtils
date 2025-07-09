namespace LSUtils;

/// <summary>
/// Represents an internal entry for managing event listeners with trigger counting and validation.
/// This class encapsulates a listener callback along with its metadata such as trigger limits,
/// remaining execution count, and validity state.
/// </summary>
/// <typeparam name="TEvent">The type of event this listener handles. Must inherit from <see cref="LSEvent"/>.</typeparam>
/// <remarks>
/// This is an internal class used by the event system to track individual listeners.
/// Each listener entry maintains its own execution state and can be configured with
/// a limited number of triggers before automatic removal.
/// </remarks>
/// <example>
/// <code>
/// // Create a listener entry that can be triggered 5 times
/// var entry = new ListenerEntry&lt;MyEvent&gt;(
///     Guid.NewGuid(), 
///     (id, evt) => Console.WriteLine($"Event received: {evt}"), 
///     5
/// );
/// </code>
/// </example>
internal class ListenerEntry<TEvent> where TEvent : LSEvent {

    #region Fields
    /// <summary>
    /// The callback function to execute when the event is triggered.
    /// </summary>
    protected LSListener<TEvent> _callback;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the unique identifier of the listener.
    /// </summary>
    /// <value>A <see cref="System.Guid"/> that uniquely identifies this listener instance.</value>
    /// <remarks>
    /// If no ID is provided during construction or an empty/default GUID is passed,
    /// a new GUID will be automatically generated.
    /// </remarks>
    public System.Guid ListenerID { get; }

    /// <summary>
    /// Gets the total number of times this listener can be triggered.
    /// </summary>
    /// <value>
    /// The maximum number of triggers allowed. A value of -1 indicates unlimited triggers.
    /// </value>
    /// <remarks>
    /// This value is set during construction and remains constant throughout the listener's lifetime.
    /// Once <see cref="TriggersRemaining"/> reaches 0, the listener will no longer execute.
    /// </remarks>
    public int TotalTriggers { get; }

    /// <summary>
    /// Gets the count of remaining triggers for this listener.
    /// </summary>
    /// <value>
    /// The number of times this listener can still be executed. 
    /// A value of -1 indicates unlimited remaining triggers.
    /// </value>
    /// <remarks>
    /// This value decreases with each successful execution. When it reaches 0,
    /// the listener is considered exhausted and will be automatically removed
    /// from the event system.
    /// </remarks>
    public int TriggersRemaining { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether this listener is in a valid state.
    /// </summary>
    /// <value><c>true</c> if the listener is valid and can be executed; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// Currently always returns <c>true</c> after construction. This property is reserved
    /// for future use cases where listeners might become invalid due to external factors.
    /// </remarks>
    public bool IsValid { get; }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="ListenerEntry{TEvent}"/> class.
    /// </summary>
    /// <param name="listenerID">
    /// The unique identifier for this listener. If <see cref="System.Guid.Empty"/> or default,
    /// a new GUID will be automatically generated.
    /// </param>
    /// <param name="callback">
    /// The callback function to execute when the event is triggered.
    /// Takes the listener ID and the event instance as parameters.
    /// </param>
    /// <param name="triggers">
    /// The maximum number of times this listener can be executed.
    /// Use -1 for unlimited triggers. Defaults to -1.
    /// </param>
    /// <remarks>
    /// The listener is automatically marked as valid upon construction.
    /// If triggers is set to a positive number, the listener will be automatically
    /// removed after that many executions.
    /// </remarks>
    internal ListenerEntry(System.Guid listenerID, LSListener<TEvent> callback, int triggers = -1) {
        ListenerID = listenerID == default || listenerID == System.Guid.Empty ? System.Guid.NewGuid() : listenerID;
        TotalTriggers = triggers;
        TriggersRemaining = triggers;
        _callback = callback;
        IsValid = true;
    }
    #endregion

    #region Core Operations
    /// <summary>
    /// Executes the listener's callback function with the provided event.
    /// </summary>
    /// <param name="event">The event to pass to the listener callback.</param>
    /// <returns>
    /// The number of remaining triggers after execution. Returns -1 for unlimited triggers,
    /// 0 if the listener is exhausted, or a positive number indicating remaining executions.
    /// </returns>
    /// <exception cref="LSException">
    /// Thrown when the listener is not valid, has no remaining triggers,
    /// or when the event type doesn't match the expected type.
    /// </exception>
    /// <remarks>
    /// This method handles trigger counting automatically. If the listener has a limited
    /// number of triggers and reaches 0 remaining triggers, it should be removed from
    /// the event system by the caller.
    /// </remarks>
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
    #endregion
}
