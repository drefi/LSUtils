namespace LSUtils;

/// <summary>
/// Abstract base class for event handling in the LSUtils system. Provides synchronization,
/// dispatching, and lifecycle management for events with semaphore-based coordination.
/// </summary>
/// <remarks>
/// Events are used for communication between different parts of the system through a dispatcher.
/// Each event maintains its own semaphore for synchronization and supports success, failure,
/// and cancellation callbacks. Events can be dispatched to registered listeners and can handle
/// timeouts and delays.
/// </remarks>
public abstract class LSEvent {
    #region Private Fields
    /// <summary>
    /// The semaphore used for synchronization and lifecycle management of the event.
    /// </summary>
    protected readonly Semaphore _semaphore;
    
    /// <summary>
    /// The error handler for processing error messages.
    /// </summary>
    protected LSMessageHandler _errorHandler;
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets the dispatcher responsible for managing event distribution.
    /// </summary>
    /// <value>The LSDispatcher instance associated with this event.</value>
    public readonly LSDispatcher Dispatcher;

    /// <summary>
    /// Gets the class name of this event instance.
    /// </summary>
    /// <value>Returns "LSEvent" by default, can be overridden by derived classes.</value>
    public virtual string ClassName => nameof(LSEvent);

    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// </summary>
    /// <value>A GUID that uniquely identifies this event.</value>
    public virtual System.Guid ID { get; }

    /// <summary>
    /// Gets the type classification of this event.
    /// </summary>
    /// <value>The LSEventType determined by the event's concrete type.</value>
    public LSEventType EventType { get; }

    /// <summary>
    /// Gets a value indicating whether this event has been dispatched to listeners.
    /// </summary>
    /// <value>
    /// <c>true</c> if the event has been dispatched; otherwise, <c>false</c>.
    /// </value>
    public bool HasDispatched { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether the event has completed successfully.
    /// </summary>
    /// <value>
    /// <c>true</c> if the event has finished execution; otherwise, <c>false</c>.
    /// </value>
    public bool IsDone => _semaphore.IsDone;

    /// <summary>
    /// Gets a value indicating whether the event has been cancelled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the event was cancelled; otherwise, <c>false</c>.
    /// </value>
    public bool IsCancelled => _semaphore.IsCancelled;

    /// <summary>
    /// Gets a value indicating whether the event encountered any failures.
    /// </summary>
    /// <value>
    /// <c>true</c> if the event failed during execution; otherwise, <c>false</c>.
    /// </value>
    public bool HasFailed => _semaphore.HasFailed;

    /// <summary>
    /// Gets the number of pending operations in the event's semaphore.
    /// </summary>
    /// <value>The count of pending signals in the underlying semaphore.</value>
    public int Count => _semaphore.Count;

    /// <summary>
    /// Gets or sets the listener group type that determines how listeners are matched.
    /// </summary>
    /// <value>
    /// The type of listener grouping strategy. Default is <see cref="ListenerGroupType.STATIC"/>.
    /// </value>
    public virtual ListenerGroupType GroupType { get; protected set; } = ListenerGroupType.STATIC;
    #endregion

    #region Events
    /// <summary>
    /// Event triggered when the event completes successfully.
    /// </summary>
    /// <remarks>
    /// This event is raised when all operations complete without failures or cancellation.
    /// Delegates the event handling to the underlying semaphore's success callback.
    /// </remarks>
    public event LSAction SuccessCallback {
        add { _semaphore.SuccessCallback += value; }
        remove { _semaphore.SuccessCallback -= value; }
    }

    /// <summary>
    /// Event triggered when the event is dispatched to listeners.
    /// </summary>
    /// <remarks>
    /// This event is raised immediately after the event is dispatched, allowing
    /// for post-dispatch processing or logging.
    /// </remarks>
    public event LSAction? DispatchCallback;

    /// <summary>
    /// Event triggered when the event is cancelled.
    /// </summary>
    /// <remarks>
    /// This event is raised when the event is explicitly cancelled, either programmatically
    /// or due to timeout conditions. Delegates to the underlying semaphore's cancel callback.
    /// </remarks>
    public event LSAction CancelCallback {
        add { _semaphore.CancelCallback += value; }
        remove { _semaphore.CancelCallback -= value; }
    }

    /// <summary>
    /// Event triggered when the event encounters a failure.
    /// </summary>
    /// <remarks>
    /// This event is raised when any failure occurs during event processing.
    /// The failure message contains details about what went wrong.
    /// Delegates to the underlying semaphore's failure callback.
    /// </remarks>
    public event LSAction<string> FailureCallback {
        add { _semaphore.FailureCallback += value; }
        remove { _semaphore.FailureCallback -= value; }
    }
    #endregion
    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEvent"/> class with the specified options.
    /// </summary>
    /// <param name="eventOptions">
    /// The configuration options for the event, including ID, dispatcher, timeout, and callbacks.
    /// </param>
    /// <remarks>
    /// Sets up the event's internal state, creates the underlying semaphore, and configures
    /// all callbacks. If a timeout is specified, it will be automatically set up.
    /// The event type is determined by the concrete class type using reflection.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="eventOptions"/> is null.
    /// </exception>
    public LSEvent(LSEventOptions eventOptions) {
        ID = eventOptions.ID;
        Dispatcher = eventOptions.Dispatcher == null ? LSDispatcher.Instance : eventOptions.Dispatcher;
        EventType = LSEventType.Get(GetType());
        _errorHandler = eventOptions.error;
        GroupType = eventOptions.GroupType;
        _semaphore = Semaphore.Create();
        _semaphore.SuccessCallback += eventOptions.success;
        _semaphore.FailureCallback += eventOptions.failure;
        _semaphore.CancelCallback += eventOptions.cancel;
        DispatchCallback += eventOptions.dispatch;
        if (eventOptions.Timeout > 0f) {
            Timeout(eventOptions.Timeout);
        }
    }
    #endregion

    #region Core Operations
    /// <summary>
    /// Dispatches this event to registered listeners through the associated dispatcher.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the event was successfully dispatched; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Creates a listener group entry based on the event type, group type, and associated instances,
    /// then dispatches to all matching listeners. Sets <see cref="HasDispatched"/> to true.
    /// After dispatching, the <see cref="DispatchCallback"/> event may be triggered.
    /// </remarks>
    public bool Dispatch() {
        ListenerGroupEntry searchGroup = ListenerGroupEntry.Create(EventType, GroupType, GetInstances());
        HasDispatched = true;
        return Dispatcher.Dispatch(searchGroup, this);
    }

    /// <summary>
    /// Gets the instances associated with this event for listener matching.
    /// </summary>
    /// <returns>
    /// An array of <see cref="ILSEventable"/> instances. The base implementation returns an empty array.
    /// </returns>
    /// <remarks>
    /// This method is used by the dispatcher to determine which listeners should receive this event.
    /// Derived classes should override this method to return their associated instances.
    /// </remarks>
    public virtual ILSEventable[] GetInstances() => new ILSEventable[0];
    #endregion

    #region Synchronization Operations
    /// <summary>
    /// Adds a wait signal to the event's semaphore, incrementing the number of pending operations.
    /// </summary>
    /// <remarks>
    /// This is a non-blocking operation that increases the semaphore count.
    /// Each call to Wait must be matched with a corresponding Signal, Failure, or Cancel call.
    /// </remarks>
    public void Wait() => _semaphore.Wait();

    /// <summary>
    /// Adds a delayed wait signal to the event's semaphore with optional callback on completion.
    /// </summary>
    /// <param name="delayValue">The delay in seconds before the signal is automatically triggered.</param>
    /// <param name="delayCallback">Optional callback to execute when the delay completes successfully.</param>
    /// <param name="signalID">Optional signal ID for tracking. If not provided, a new GUID is generated.</param>
    /// <remarks>
    /// If the delay value is less than or equal to 0, the method returns immediately without effect.
    /// The delay callback is added to the success callback chain and will be called if the event
    /// completes successfully. The dispatcher handles the actual delay timing.
    /// </remarks>
    public void Wait(float delayValue, LSAction? delayCallback = null, System.Guid signalID = default) {
        if (delayValue <= 0f) return;
        _semaphore.Wait(signalID);
        if (delayCallback != null) SuccessCallback += delayCallback;
        Dispatcher.Delay(delayValue, _semaphore.Signal);
    }

    /// <summary>
    /// Sets up a timeout for the event that will cause failure if the event doesn't complete in time.
    /// </summary>
    /// <param name="delayValue">The timeout duration in seconds.</param>
    /// <remarks>
    /// If the delay value is less than or equal to 0, no timeout is set.
    /// When the timeout expires, if the event is still pending, it will be marked as failed
    /// with a timeout message and cancelled if it was the last pending operation.
    /// </remarks>
    public void Timeout(float delayValue) {
        if (delayValue <= 0f) return;
        Dispatcher.Delay(delayValue, () => {
            if (IsDone || IsCancelled) return;
            if (_semaphore.Failure(out _, "{{timeout}}") > 0)
                _semaphore.Cancel();
        });
    }

    /// <summary>
    /// Signals the completion of one operation and triggers the dispatch callback.
    /// </summary>
    /// <returns>The number of remaining signals in the semaphore queue.</returns>
    /// <remarks>
    /// This method is typically called internally by the system after event processing.
    /// It combines the semaphore signal with the dispatch callback notification.
    /// </remarks>
    internal int done() {
        int signal = _semaphore.Signal(out _);
        DispatchCallback?.Invoke();
        return signal;
    }

    /// <summary>
    /// Signals the completion of one operation without additional processing.
    /// </summary>
    /// <remarks>
    /// This is a direct pass-through to the underlying semaphore's Signal method.
    /// Use this when you need to manually signal completion of an operation.
    /// </remarks>
    public void Signal() => _semaphore.Signal();

    /// <summary>
    /// Signals the completion of one operation and returns the processed signal ID.
    /// </summary>
    /// <param name="signalID">The GUID of the signal that was processed.</param>
    /// <returns>The number of remaining signals in the semaphore queue.</returns>
    /// <remarks>
    /// This overload provides access to the signal ID that was processed,
    /// which can be useful for tracking specific operations.
    /// </remarks>
    public int Signal(out System.Guid signalID) => _semaphore.Signal(out signalID);
    #endregion
    #region Error Handling and Cancellation
    /// <summary>
    /// Reports a failure for the current operation with an optional cancellation.
    /// </summary>
    /// <param name="msg">The failure message to record.</param>
    /// <param name="cancel">
    /// If <c>true</c>, cancels the event after recording the failure;
    /// if <c>false</c>, continues normal operation after recording the failure.
    /// </param>
    /// <remarks>
    /// The failure message is added to the event's failure collection and may be
    /// included in the final failure callback when the event completes or is cancelled.
    /// </remarks>
    public void Failure(string msg, bool cancel = false) => _semaphore.Failure(msg, cancel);

    /// <summary>
    /// Reports a failure for the current operation and returns the processed signal ID.
    /// </summary>
    /// <param name="signalID">The GUID of the signal that was processed.</param>
    /// <param name="msg">The failure message to record.</param>
    /// <returns>The number of remaining signals in the semaphore queue.</returns>
    /// <remarks>
    /// This overload provides access to the signal ID that was processed while recording the failure.
    /// The failure message will be included in any failure callbacks that are triggered.
    /// </remarks>
    public int Failure(out System.Guid signalID, string msg) => _semaphore.Failure(out signalID, msg);

    /// <summary>
    /// Cancels the event, clearing all pending operations and triggering cancellation callbacks.
    /// </summary>
    /// <remarks>
    /// When cancelled, all remaining signals are cleared and the <see cref="CancelCallback"/>
    /// is triggered. If there were any failures, the <see cref="FailureCallback"/> may also be triggered.
    /// </remarks>
    public void Cancel() => _semaphore.Cancel();

    /// <summary>
    /// Cancels the event and returns the IDs of all remaining signals that were cleared.
    /// </summary>
    /// <param name="remainingSignalIDs">
    /// An array containing the GUIDs of all signals that were pending when cancellation occurred.
    /// </param>
    /// <returns>The number of signals that were cancelled.</returns>
    /// <remarks>
    /// This overload provides detailed information about which operations were cancelled,
    /// which can be useful for cleanup or logging purposes.
    /// </remarks>
    public int Cancel(out System.Guid[] remainingSignalIDs) => _semaphore.Cancel(out remainingSignalIDs);
    #endregion
}

/// <summary>
/// Generic event class that associates an event with a specific instance type.
/// </summary>
/// <typeparam name="TInstance">
/// The type of the primary instance associated with this event. Must implement <see cref="ILSEventable"/>.
/// </typeparam>
/// <remarks>
/// This class extends the base <see cref="LSEvent"/> to provide strongly-typed access to
/// event instances. It maintains an array of instances and provides convenient access
/// to the primary instance through the <see cref="Instance"/> property.
/// </remarks>
public abstract class LSEvent<TInstance> : LSEvent where TInstance : ILSEventable {
    #region Protected Fields
    /// <summary>
    /// Array of instances associated with this event.
    /// </summary>
    protected ILSEventable[] _instances;
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets the primary instance associated with this event.
    /// </summary>
    /// <value>
    /// The first instance in the instances array, cast to the specified type,
    /// or the default value if no instances are present.
    /// </value>
    public TInstance Instance => _instances.Length > 0 ? (TInstance)_instances[0] : default(TInstance)!;
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEvent{TInstance}"/> class with multiple instances.
    /// </summary>
    /// <param name="instances">An array of instances to associate with this event.</param>
    /// <param name="eventOptions">The configuration options for the event.</param>
    /// <remarks>
    /// This constructor allows creating an event that is associated with multiple instances.
    /// The first instance in the array becomes the primary instance accessible through
    /// the <see cref="Instance"/> property.
    /// </remarks>
    protected LSEvent(ILSEventable[] instances, LSEventIOptions eventOptions) : base(eventOptions) {
        _instances = instances;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSEvent{TInstance}"/> class with a single instance.
    /// </summary>
    /// <param name="instance">The instance to associate with this event.</param>
    /// <param name="eventOptions">The configuration options for the event.</param>
    /// <remarks>
    /// This constructor creates an event associated with a single instance, which becomes
    /// both the primary instance and the only instance in the instances array.
    /// </remarks>
    protected LSEvent(ILSEventable instance, LSEventIOptions eventOptions) : base(eventOptions) {
        _instances = new ILSEventable[] { instance };
    }
    #endregion

    #region Overridden Methods
    /// <summary>
    /// Gets the instances associated with this event for listener matching.
    /// </summary>
    /// <returns>The array of instances associated with this event.</returns>
    /// <remarks>
    /// Overrides the base implementation to return the actual instances associated with this event,
    /// which are used by the dispatcher for listener matching and event routing.
    /// </remarks>
    public override ILSEventable[] GetInstances() {
        return _instances;
    }
    #endregion
}

/// <summary>
/// Generic event class that associates an event with both primary and secondary instance types.
/// </summary>
/// <typeparam name="TPrimaryInstance">
/// The type of the primary instance. Must implement <see cref="ILSEventable"/>.
/// </typeparam>
/// <typeparam name="TSecondaryInstance">
/// The type of the secondary instance. Must implement <see cref="ILSEventable"/>.
/// </typeparam>
/// <remarks>
/// This class extends <see cref="LSEvent{TInstance}"/> to provide strongly-typed access to
/// both a primary and secondary instance. This is useful for events that involve interaction
/// between two specific objects or entities.
/// </remarks>
public abstract class LSEvent<TPrimaryInstance, TSecondaryInstance> : LSEvent<TPrimaryInstance> 
    where TPrimaryInstance : ILSEventable 
    where TSecondaryInstance : ILSEventable {
    
    #region Public Properties
    /// <summary>
    /// Gets the secondary instance associated with this event.
    /// </summary>
    /// <value>The second instance in the instances array, cast to the specified type.</value>
    /// <exception cref="LSException">
    /// Thrown when the secondary instance is null or not available.
    /// </exception>
    /// <remarks>
    /// Provides strongly-typed access to the secondary instance. The secondary instance
    /// is expected to be at index 1 in the instances array.
    /// </remarks>
    public TSecondaryInstance SecondaryInstance => (TSecondaryInstance)_instances[1] ?? throw new LSException($"{{secondary_instance_null}}");
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEvent{TPrimaryInstance, TSecondaryInstance}"/> class.
    /// </summary>
    /// <param name="primaryInstance">The primary instance to associate with this event.</param>
    /// <param name="secondaryInstance">The secondary instance to associate with this event.</param>
    /// <param name="eventOptions">The configuration options for the event.</param>
    /// <exception cref="LSArgumentNullException">
    /// Thrown when either <paramref name="primaryInstance"/> or <paramref name="secondaryInstance"/> is null.
    /// </exception>
    /// <remarks>
    /// Both instances are required and cannot be null. They are stored in the instances array
    /// with the primary instance at index 0 and the secondary instance at index 1.
    /// </remarks>
    protected LSEvent(TPrimaryInstance primaryInstance, TSecondaryInstance secondaryInstance, LSEventIOptions eventOptions) 
        : base(new ILSEventable[] { primaryInstance, secondaryInstance }, eventOptions) {
        if (primaryInstance == null) throw new LSArgumentNullException(nameof(primaryInstance), "{primary_instance_null}");
        if (secondaryInstance == null) throw new LSArgumentNullException(nameof(secondaryInstance), "{secondary_instance_null}");
    }
    #endregion
}
/// <summary>
/// Abstract base class for initialization events that are triggered when an object is initialized.
/// </summary>
/// <remarks>
/// Initialization events are commonly used to notify listeners when objects or systems
/// are being set up or configured. This base class provides factory methods for creating
/// and registering initialization events for specific instance types.
/// </remarks>
public abstract class OnInitializeEvent : LSEvent<ILSEventable> {
    #region Static Factory Methods
    /// <summary>
    /// Creates a new initialization event for the specified instance.
    /// </summary>
    /// <typeparam name="TInstance">The type of instance being initialized.</typeparam>
    /// <param name="instance">The instance that is being initialized.</param>
    /// <param name="eventOptions">
    /// Optional configuration for the event. If null, default options will be used.
    /// </param>
    /// <returns>A new <see cref="OnInitializeEvent{TInstance}"/> for the specified instance.</returns>
    /// <remarks>
    /// This factory method provides a convenient way to create strongly-typed initialization events.
    /// If no event options are provided, a default <see cref="LSEventIOptions"/> instance is created.
    /// </remarks>
    public static OnInitializeEvent<TInstance> Create<TInstance>(TInstance instance, LSEventIOptions? eventOptions) where TInstance : ILSEventable {
        eventOptions ??= new LSEventIOptions();
        return OnInitializeEvent<TInstance>.Create(instance, eventOptions);
    }

    /// <summary>
    /// Registers a listener for initialization events of the specified type.
    /// </summary>
    /// <typeparam name="TInstance">The type of instance the listener is interested in.</typeparam>
    /// <param name="listener">The listener callback to register.</param>
    /// <param name="instances">
    /// Optional array of specific instances to listen for. If null, listens to all instances of the type.
    /// </param>
    /// <param name="triggers">
    /// The number of times the listener should be triggered. Use -1 for unlimited triggers.
    /// </param>
    /// <param name="listenerID">
    /// Optional unique identifier for the listener. If not provided, a new GUID is generated.
    /// </param>
    /// <param name="dispatcher">
    /// Optional dispatcher to register with. If null, uses the default instance.
    /// </param>
    /// <returns>The unique identifier of the registered listener.</returns>
    /// <remarks>
    /// This method provides a convenient way to register listeners for initialization events
    /// without needing to manually interact with the dispatcher.
    /// </remarks>
    public static System.Guid Register<TInstance>(LSListener<OnInitializeEvent<TInstance>> listener, ILSEventable[] instances = null!, int triggers = -1, System.Guid listenerID = default, LSDispatcher? dispatcher = null) where TInstance : ILSEventable {
        return OnInitializeEvent<TInstance>.Register(listener, instances, triggers, listenerID, dispatcher);
    }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="OnInitializeEvent"/> class.
    /// </summary>
    /// <param name="instance">The instance being initialized.</param>
    /// <param name="options">The configuration options for the event.</param>
    /// <remarks>
    /// This constructor is used by derived classes to create initialization events
    /// for specific instance types.
    /// </remarks>
    protected OnInitializeEvent(ILSEventable instance, LSEventIOptions options) : base(instance, options) { }
    #endregion
}

/// <summary>
/// Strongly-typed initialization event for a specific instance type.
/// </summary>
/// <typeparam name="TInstance">
/// The type of instance being initialized. Must implement <see cref="ILSEventable"/>.
/// </typeparam>
/// <remarks>
/// This class provides a concrete implementation of initialization events that is strongly-typed
/// to a specific instance type. It includes factory methods for creation and listener registration
/// that are specific to the instance type.
/// </remarks>
public class OnInitializeEvent<TInstance> : OnInitializeEvent where TInstance : ILSEventable {
    #region Static Factory Methods
    /// <summary>
    /// Creates a new strongly-typed initialization event for the specified instance.
    /// </summary>
    /// <param name="instance">The instance that is being initialized.</param>
    /// <param name="eventOptions">The configuration options for the event.</param>
    /// <returns>A new <see cref="OnInitializeEvent{TInstance}"/> for the specified instance.</returns>
    /// <remarks>
    /// This factory method creates a concrete initialization event that is strongly-typed
    /// to the specified instance type, providing type safety and better IntelliSense support.
    /// </remarks>
    public static OnInitializeEvent<TInstance> Create(TInstance instance, LSEventIOptions eventOptions) {
        OnInitializeEvent<TInstance> @event = new OnInitializeEvent<TInstance>(instance, eventOptions);
        return @event;
    }

    /// <summary>
    /// Registers a strongly-typed listener for initialization events of this specific type.
    /// </summary>
    /// <param name="listener">The strongly-typed listener callback to register.</param>
    /// <param name="instances">
    /// Optional array of specific instances to listen for. If null, listens to all instances of the type.
    /// </param>
    /// <param name="triggers">
    /// The number of times the listener should be triggered. Use -1 for unlimited triggers.
    /// </param>
    /// <param name="listenerID">
    /// Optional unique identifier for the listener. If not provided, a new GUID is generated.
    /// </param>
    /// <param name="dispatcher">
    /// Optional dispatcher to register with. If null, uses the default instance.
    /// </param>
    /// <returns>The unique identifier of the registered listener.</returns>
    /// <remarks>
    /// This method registers a listener that will only receive initialization events for the
    /// specific instance type <typeparamref name="TInstance"/>, providing type safety.
    /// </remarks>
    public static System.Guid Register(LSListener<OnInitializeEvent<TInstance>> listener, ILSEventable[] instances = null!, int triggers = -1, System.Guid listenerID = default, LSDispatcher? dispatcher = null) {
        dispatcher ??= LSDispatcher.Instance;
        return dispatcher.Register<OnInitializeEvent<TInstance>>(listener, instances, triggers, listenerID);
    }
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets the strongly-typed instance being initialized.
    /// </summary>
    /// <value>The instance cast to the specific type <typeparamref name="TInstance"/>.</value>
    /// <remarks>
    /// This property provides strongly-typed access to the instance, hiding the base class's
    /// generic instance property and providing better type safety and IntelliSense support.
    /// </remarks>
    public new TInstance Instance => (TInstance)base.Instance!;
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="OnInitializeEvent{TInstance}"/> class.
    /// </summary>
    /// <param name="instance">The strongly-typed instance being initialized.</param>
    /// <param name="eventOptions">The configuration options for the event.</param>
    /// <remarks>
    /// This constructor creates a strongly-typed initialization event for the specified instance.
    /// </remarks>
    protected OnInitializeEvent(TInstance instance, LSEventIOptions eventOptions) : base(instance, eventOptions) { }
    #endregion
}
/// <summary>
/// Configuration options for instance-based events that use subset listener grouping.
/// </summary>
/// <remarks>
/// This class extends <see cref="LSEventOptions"/> to provide default configuration
/// specifically for events that are associated with instances and use subset-based
/// listener matching. The group type is set to <see cref="ListenerGroupType.SUBSET"/>
/// by default, which is appropriate for events tied to specific object instances.
/// </remarks>
public class LSEventIOptions : LSEventOptions {
    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventIOptions"/> class with default settings.
    /// </summary>
    /// <remarks>
    /// Creates event options with subset-based listener grouping, which is appropriate
    /// for events that are associated with specific instances.
    /// </remarks>
    public LSEventIOptions() : base() { }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the listener group type for instance-based events.
    /// </summary>
    /// <value>
    /// The group type used for listener matching. Defaults to <see cref="ListenerGroupType.SUBSET"/>.
    /// </value>
    /// <remarks>
    /// Overrides the base class default to use subset grouping, which is more appropriate
    /// for events that are tied to specific object instances rather than global events.
    /// </remarks>
    public override ListenerGroupType GroupType { get; set; } = ListenerGroupType.SUBSET;
    #endregion
}

/// <summary>
/// Base configuration options for LSEvent instances, including callbacks, timing, and dispatcher settings.
/// </summary>
/// <remarks>
/// This class provides comprehensive configuration for events, including success/failure/cancellation
/// callbacks, timeout settings, custom dispatchers, and error handling. It serves as the foundation
/// for all event configuration in the LSUtils system.
/// </remarks>
public class LSEventOptions {
    #region Private Fields
    /// <summary>
    /// Internal event handlers for the various event lifecycle callbacks.
    /// </summary>
    public event LSMessageHandler? ErrorHandler;
    public event LSAction? OnDispatch;
    public event LSAction? OnSuccess;
    public event LSAction<string>? OnFailure;
    public event LSAction? OnCancel;
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventOptions"/> class with default settings.
    /// </summary>
    /// <remarks>
    /// Creates event options with a new unique ID, no timeout, the default dispatcher,
    /// and static listener grouping.
    /// </remarks>
    public LSEventOptions() { }
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets or sets the unique identifier for the event.
    /// </summary>
    /// <value>A GUID that uniquely identifies the event. Defaults to a new GUID.</value>
    public System.Guid ID { get; set; } = System.Guid.NewGuid();

    /// <summary>
    /// Gets or sets the timeout duration for the event in seconds.
    /// </summary>
    /// <value>
    /// The timeout in seconds. A value of 0 or less means no timeout is applied.
    /// Default is 0 (no timeout).
    /// </value>
    /// <remarks>
    /// When a timeout is set, the event will automatically fail with a timeout message
    /// if it doesn't complete within the specified duration.
    /// </remarks>
    public float Timeout { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the dispatcher to use for this event.
    /// </summary>
    /// <value>
    /// The LSDispatcher instance to handle event dispatching. Defaults to the singleton instance.
    /// </value>
    /// <remarks>
    /// The dispatcher is responsible for routing events to registered listeners and managing
    /// the event lifecycle. Most events can use the default dispatcher instance.
    /// </remarks>
    public LSDispatcher Dispatcher { get; set; } = LSDispatcher.Instance;

    /// <summary>
    /// Gets or sets the listener group type that determines how listeners are matched.
    /// </summary>
    /// <value>
    /// The grouping strategy for listener matching. Defaults to <see cref="ListenerGroupType.STATIC"/>.
    /// </value>
    /// <remarks>
    /// The group type determines how the dispatcher matches this event with registered listeners.
    /// Static grouping matches by event type only, while subset grouping also considers instances.
    /// </remarks>
    public virtual ListenerGroupType GroupType { get; set; } = ListenerGroupType.STATIC;
    #endregion

    #region Internal Callback Methods
    /// <summary>
    /// Triggers the dispatch callback if one is registered.
    /// </summary>
    /// <remarks>
    /// This method is called internally when the event is dispatched to listeners.
    /// It's part of the event lifecycle management system.
    /// </remarks>
    internal void dispatch() => OnDispatch?.Invoke();

    /// <summary>
    /// Triggers the success callback if one is registered.
    /// </summary>
    /// <remarks>
    /// This method is called internally when the event completes successfully.
    /// It's part of the event lifecycle management system.
    /// </remarks>
    internal void success() => OnSuccess?.Invoke();

    /// <summary>
    /// Triggers the failure callback with the specified message if one is registered.
    /// </summary>
    /// <param name="msg">The failure message to pass to the callback.</param>
    /// <remarks>
    /// This method is called internally when the event encounters a failure.
    /// It's part of the event lifecycle management system.
    /// </remarks>
    internal void failure(string msg) => OnFailure?.Invoke(msg);

    /// <summary>
    /// Triggers the cancellation callback if one is registered.
    /// </summary>
    /// <remarks>
    /// This method is called internally when the event is cancelled.
    /// It's part of the event lifecycle management system.
    /// </remarks>
    internal void cancel() => OnCancel?.Invoke();

    /// <summary>
    /// Handles error conditions using the registered error handler.
    /// </summary>
    /// <param name="msg">The error message to handle.</param>
    /// <returns>
    /// <c>true</c> if the error was handled successfully; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="LSException">
    /// Thrown when no error handler is registered.
    /// </exception>
    /// <remarks>
    /// This method provides a centralized way to handle errors that occur during event processing.
    /// If no error handler is registered, an exception is thrown with the error details.
    /// </remarks>
    internal bool error(string? msg) {
        if (ErrorHandler == null) {
            throw new LSException($"no_error_handler:{msg}");
        }
        return ErrorHandler(msg);
    }
    #endregion
}
