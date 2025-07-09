using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils;

/// <summary>
/// Central event dispatcher responsible for managing and routing events to registered listeners.
/// Provides thread-safe event registration, dispatching, and lifecycle management with support
/// for instance-based and static event routing strategies.
/// </summary>
/// <remarks>
/// The LSDispatcher serves as the core communication hub for the event system. It maintains
/// a collection of listener groups organized by event type and instance associations, supports
/// both singleton and custom instances, and provides delay handling capabilities for timed operations.
/// The dispatcher is thread-safe and supports concurrent operations through internal locking mechanisms.
/// </remarks>
public partial class LSDispatcher {
    #region Static Members
    /// <summary>
    /// The singleton instance of the dispatcher.
    /// </summary>
    static LSDispatcher? _instance;
    
    /// <summary>
    /// Thread synchronization lock for singleton creation.
    /// </summary>
    private readonly object _lockObj = new object();

    /// <summary>
    /// Gets the singleton instance of the <see cref="LSDispatcher"/>.
    /// </summary>
    /// <value>
    /// The global dispatcher instance used for event routing when no custom dispatcher is specified.
    /// </value>
    /// <remarks>
    /// The singleton instance is created lazily on first access and uses a default delay handler
    /// that executes callbacks immediately. For custom delay handling, create a specific dispatcher instance.
    /// </remarks>
    public static LSDispatcher Instance {
        get {
            if (_instance == null) _instance = new LSDispatcher();
            return _instance;
        }
    }
    #endregion

    #region Private Fields
    /// <summary>
    /// Thread-safe dictionary storing listener groups indexed by their hash codes.
    /// </summary>
    /// <remarks>
    /// Each entry represents a unique combination of event type, group type, and associated instances.
    /// The hash code serves as an efficient lookup key for matching events to listener groups.
    /// </remarks>
    readonly ConcurrentDictionary<int, ListenerGroupEntry> _listeners = new ConcurrentDictionary<int, ListenerGroupEntry>();
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets the class name of this dispatcher instance.
    /// </summary>
    /// <value>Returns "LSDispatcher".</value>
    public string ClassName => nameof(LSDispatcher);

    /// <summary>
    /// Gets or sets the delay handler used for timed operations.
    /// </summary>
    /// <value>
    /// A delegate that accepts a delay duration and a callback to execute after the delay.
    /// </value>
    /// <remarks>
    /// The delay handler is responsible for implementing timing functionality for events
    /// that require delayed execution or timeout behavior. The default implementation
    /// executes callbacks immediately without any delay.
    /// </remarks>
    public LSAction<float, LSAction> Delay { get; protected set; }
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="LSDispatcher"/> class with default delay handling.
    /// </summary>
    /// <remarks>
    /// Creates a dispatcher with a default delay handler that executes callbacks immediately
    /// without any actual delay. This constructor is used for the singleton instance and
    /// when immediate callback execution is desired.
    /// </remarks>
    protected LSDispatcher() {
        Delay = (delay, callback) => callback?.Invoke();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSDispatcher"/> class with a custom delay handler.
    /// </summary>
    /// <param name="delayHandler">
    /// The custom delay handler to use for timed operations. This handler should implement
    /// the actual delay mechanism and call the provided callback after the specified duration.
    /// </param>
    /// <remarks>
    /// Use this constructor when you need custom timing behavior, such as integration with
    /// game engines, UI frameworks, or other timing systems that provide their own delay mechanisms.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example with a custom delay handler
    /// var dispatcher = new LSDispatcher((delay, callback) => {
    ///     Timer.SetTimeout(() => callback?.Invoke(), delay * 1000);
    /// });
    /// </code>
    /// </example>
    public LSDispatcher(LSAction<float, LSAction> delayHandler) {
        Delay = delayHandler;
    }
    #endregion

    #region Core Dispatching
    /// <summary>
    /// Dispatches an event to all matching registered listeners.
    /// </summary>
    /// <param name="searchGroup">
    /// The listener group criteria used to find matching listeners based on event type,
    /// group type, and associated instances.
    /// </param>
    /// <param name="event">The event to dispatch to matching listeners.</param>
    /// <returns>
    /// <c>true</c> if the event was successfully processed and completed;
    /// <c>false</c> if any listener indicated failure during processing.
    /// </returns>
    /// <exception cref="LSException">
    /// Thrown when the event is null, already cancelled, or already completed.
    /// </exception>
    /// <remarks>
    /// The dispatching process involves finding all listener groups that match the search criteria,
    /// notifying all listeners in those groups, and then marking the event as completed.
    /// If any listener indicates failure during notification, the entire dispatch operation fails.
    /// </remarks>
    internal bool Dispatch(ListenerGroupEntry searchGroup, LSEvent @event) {
        if (@event == null) throw new LSException("{{event_null}}");
        if (@event.IsCancelled || @event.IsDone) throw new LSException($"{{event{(@event.IsCancelled ? "_cancelled" : "_done")}}}");
        LSEventType eventType = LSEventType.Get(@event.GetType());
        var matches = _listeners.Where(entry => entry.Value.Contains(searchGroup));
        foreach (var match in matches) {
            if (match.Value.NotifyListeners(@event) == false) return false;
        }
        return @event.done() == 0;
    }
    #endregion

    #region Listener Registration
    /// <summary>
    /// Registers a listener for events of a specific type with detailed configuration options.
    /// </summary>
    /// <param name="listener">The listener callback to register for event notifications.</param>
    /// <param name="eventType">The type of events this listener should receive.</param>
    /// <param name="instances">
    /// Optional array of specific instances to listen for. If null or empty, uses static grouping.
    /// For subset grouping, only events from these specific instances will trigger the listener.
    /// </param>
    /// <param name="triggers">
    /// The maximum number of times this listener should be invoked. Use -1 for unlimited triggers.
    /// The listener is automatically unregistered after reaching the trigger limit.
    /// </param>
    /// <param name="listenerID">
    /// Optional unique identifier for the listener. If not provided, a new GUID is generated.
    /// This ID can be used later for unregistration or tracking purposes.
    /// </param>
    /// <returns>The unique identifier of the registered listener.</returns>
    /// <exception cref="LSException">
    /// Thrown when the listener is null, the event type is invalid, or registration fails.
    /// </exception>
    /// <remarks>
    /// This method creates or finds the appropriate listener group based on the event type,
    /// instance array, and grouping strategy. The listener is added to the group and will
    /// receive notifications for matching events until it reaches its trigger limit or is unregistered.
    /// </remarks>
    public System.Guid Register(LSListener<LSEvent> listener, LSEventType eventType, ILSEventable[]? instances = null, int triggers = -1, System.Guid listenerID = default) {
        if (listener == null) throw new LSException("listener_null");
        ListenerGroupType groupType = ListenerGroupEntry.GetGroupType(instances);
        ListenerGroupEntry group = ListenerGroupEntry.Create(eventType, groupType, instances);
        if (_listeners.TryGetValue(group.GetHashCode(), out var existingGroup) == false) {
            if (_listeners.TryAdd(group.GetHashCode(), group) == false) throw new LSException($"{{add_new_group_failed_{groupType}}}:{group.GetHashCode()}");
        } else group = existingGroup;
        if (listenerID == default || listenerID == System.Guid.Empty) listenerID = System.Guid.NewGuid();
        if (group.AddListener(listenerID, listener, triggers) == false) throw new LSException($"{{group_{group.GroupType}_failed_register_listener}}:{listenerID}");
        return listenerID;
    }

    /// <summary>
    /// Registers a strongly-typed listener for events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The specific event type to listen for. Must inherit from <see cref="LSEvent"/>.
    /// </typeparam>
    /// <param name="listener">
    /// The strongly-typed listener callback that will receive events of type <typeparamref name="TEvent"/>.
    /// </param>
    /// <param name="instances">
    /// Optional array of specific instances to listen for. If null or empty, uses static grouping.
    /// </param>
    /// <param name="triggers">
    /// The maximum number of times this listener should be invoked. Use -1 for unlimited triggers.
    /// </param>
    /// <param name="listenerID">
    /// Optional unique identifier for the listener. If not provided, a new GUID is generated.
    /// </param>
    /// <returns>The unique identifier of the registered listener.</returns>
    /// <exception cref="LSException">
    /// Thrown when the listener is null or registration fails.
    /// </exception>
    /// <remarks>
    /// This generic overload provides type safety by automatically determining the event type
    /// from the generic parameter and wrapping the strongly-typed listener in a compatible format.
    /// This is the preferred method for registering listeners when you know the specific event type at compile time.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Register a listener for OnInitializeEvent
    /// var listenerId = dispatcher.Register&lt;OnInitializeEvent&lt;MyComponent&gt;&gt;((id, evt) => {
    ///     Console.WriteLine($"Component {evt.Instance.Name} initialized");
    /// });
    /// </code>
    /// </example>
    public System.Guid Register<TEvent>(LSListener<TEvent> listener, ILSEventable[]? instances = null, int triggers = -1, System.Guid listenerID = default) where TEvent : LSEvent {
        if (listener == null) throw new LSException("listener_null");
        return Register(
            new LSListener<LSEvent>((id, e) => listener(id, (TEvent)(object)e)),
            LSEventType.Get<TEvent>(), instances, triggers, listenerID
        );
    }
    #endregion

    #region Listener Unregistration
    /// <summary>
    /// Unregisters a listener using its unique identifier.
    /// </summary>
    /// <param name="listenerID">The unique identifier of the listener to remove.</param>
    /// <returns>
    /// <c>true</c> if the listener was successfully unregistered; <c>false</c> if the listener was not found.
    /// </returns>
    /// <exception cref="LSException">
    /// Thrown when no listeners exist with the specified ID or when removal fails.
    /// </exception>
    /// <remarks>
    /// This method searches through all listener groups to find and remove the specified listener.
    /// If removing the listener leaves a group empty, the entire group is removed from the dispatcher.
    /// This cleanup helps maintain optimal performance by avoiding empty group lookups.
    /// </remarks>
    public bool Unregister(System.Guid listenerID) {
        var match = _listeners.Where(g => g.Value.GetListeners().Contains(listenerID));
        if (!_listeners.Any(g => g.Value.GetListeners().Contains(listenerID))) {
            throw new LSException($"{{no_listeners_for_id}}:{listenerID}");
        }
        foreach (var entry in match) {
            var group = entry.Value;
            if (group.RemoveListener(listenerID) == false) {
                throw new LSException($"{{failed_remove_listener}}:{listenerID}");
            }
            if (group.GetListenersCount() == 0) {
                _listeners.TryRemove(entry.Key, out _);
            }
        }
        return true;
    }

    /// <summary>
    /// Unregisters all listeners for a specific event type and instance combination.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The specific event type to unregister listeners for. Must inherit from <see cref="LSEvent"/>.
    /// </typeparam>
    /// <param name="instances">
    /// Optional array of specific instances. If provided, only listeners registered for these
    /// specific instances will be unregistered. If null, unregisters static listeners for the event type.
    /// </param>
    /// <returns>
    /// <c>true</c> if any listeners were successfully unregistered; <c>false</c> if no matching listeners were found.
    /// </returns>
    /// <remarks>
    /// This method removes entire listener groups that match the specified event type and instance criteria.
    /// It's useful for cleanup when you no longer need any listeners for a particular event/instance combination.
    /// The method is safe to call even if no matching listeners exist.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Unregister all listeners for initialization events of MyComponent
    /// dispatcher.Unregister&lt;OnInitializeEvent&lt;MyComponent&gt;&gt;();
    /// 
    /// // Unregister listeners for specific instances
    /// dispatcher.Unregister&lt;OnInitializeEvent&lt;MyComponent&gt;&gt;(new[] { myComponentInstance });
    /// </code>
    /// </example>
    public bool Unregister<TEvent>(ILSEventable[]? instances = null) where TEvent : LSEvent {
        LSEventType eventType = LSEventType.Get<TEvent>();
        ListenerGroupType groupType = ListenerGroupEntry.GetGroupType(instances);
        ListenerGroupEntry search = ListenerGroupEntry.Create(eventType, groupType, instances);
        var match = _listeners.Where(entry => entry.Value.Contains(search)).ToList();
        if (match.Any() == false) {
            return false;
        }
        bool removedAny = false;
        foreach (var entry in match) {
            if (_listeners.TryRemove(entry.Key, out _)) {
                removedAny = true;
            }
        }
        return removedAny;
    }
    #endregion
    #region Listener Querying
    /// <summary>
    /// Gets the total number of listeners registered for a specific event type and grouping criteria.
    /// </summary>
    /// <param name="eventType">The type of events to count listeners for.</param>
    /// <param name="groupType">
    /// The listener grouping strategy to search for (static or subset-based).
    /// </param>
    /// <param name="instances">
    /// Optional array of specific instances. When provided with subset grouping,
    /// counts only listeners registered for these specific instances.
    /// </param>
    /// <returns>
    /// The total number of listeners that match the specified criteria across all matching groups.
    /// </returns>
    /// <remarks>
    /// This method performs a parallel search across all listener groups to efficiently count
    /// matching listeners. It's useful for diagnostics, debugging, and determining if any
    /// listeners are registered for specific event scenarios.
    /// </remarks>
    public int GetListenersCount(LSEventType eventType, ListenerGroupType groupType, ILSEventable[]? instances = null) {
        var searchGroup = ListenerGroupEntry.Create(eventType, groupType, instances);
        return _listeners
            .AsParallel()
            .Where(entry => entry.Value.Contains(searchGroup))
            .Sum(listener => listener.Value.GetListenersCount());
    }

    /// <summary>
    /// Gets the number of listeners registered for a specific strongly-typed event and grouping criteria.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The specific event type to count listeners for. Must inherit from <see cref="LSEvent"/>.
    /// </typeparam>
    /// <param name="groupType">
    /// The listener grouping strategy to search for (static or subset-based).
    /// </param>
    /// <param name="instances">
    /// Optional array of specific instances. When provided with subset grouping,
    /// counts only listeners registered for these specific instances.
    /// </param>
    /// <returns>
    /// The total number of listeners registered for the specified event type and criteria.
    /// </returns>
    /// <remarks>
    /// This generic overload provides type safety and convenience by automatically determining
    /// the event type from the generic parameter. It's the preferred method when you know
    /// the specific event type at compile time.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Count all static listeners for initialization events
    /// int staticCount = dispatcher.GetListenersCount&lt;OnInitializeEvent&lt;MyComponent&gt;&gt;(
    ///     ListenerGroupType.STATIC);
    /// 
    /// // Count subset listeners for specific instances
    /// int subsetCount = dispatcher.GetListenersCount&lt;OnInitializeEvent&lt;MyComponent&gt;&gt;(
    ///     ListenerGroupType.SUBSET, new[] { myComponentInstance });
    /// </code>
    /// </example>
    public int GetListenersCount<TEvent>(ListenerGroupType groupType, ILSEventable[]? instances = null) where TEvent : LSEvent {
        LSEventType eventType = LSEventType.Get(typeof(TEvent));
        return GetListenersCount(eventType, groupType, instances);
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Sets a custom delay handler for timed operations.
    /// </summary>
    /// <param name="delayHandler">
    /// The new delay handler to use. This handler should implement the actual delay mechanism
    /// and call the provided callback after the specified duration.
    /// </param>
    /// <remarks>
    /// Changing the delay handler affects all future timed operations performed by this dispatcher.
    /// This is useful for integrating with different timing systems or changing behavior at runtime.
    /// Existing delayed operations that are already scheduled will not be affected.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Set a custom delay handler that uses a game engine's timer
    /// dispatcher.SetDelayHandler((delay, callback) => {
    ///     GameEngine.ScheduleCallback(delay, callback);
    /// });
    /// </code>
    /// </example>
    public void SetDelayHandler(LSAction<float, LSAction> delayHandler) {
        Delay = delayHandler;
    }
    #endregion


    #region Diagnostic Support
    /// <summary>
    /// Defines diagnostic modes for debugging and monitoring dispatcher behavior.
    /// </summary>
    /// <remarks>
    /// These flags can be combined to enable multiple diagnostic features simultaneously.
    /// Diagnostic modes are typically used during development and debugging to understand
    /// event flow and identify issues in the event system.
    /// </remarks>
    [System.Flags]
    public enum DiagnosticMode {
        /// <summary>
        /// No diagnostic output is generated.
        /// </summary>
        NONE = 0,
        
        /// <summary>
        /// Enables verbose logging of all event operations and listener interactions.
        /// </summary>
        VERBOSE = 1,
        
        /// <summary>
        /// Enables logging of warning conditions and potential issues.
        /// </summary>
        WARNINGS = 2,
        
        /// <summary>
        /// Enables logging of error conditions and failures.
        /// </summary>
        ERRORS = 4
    }

    /// <summary>
    /// The current global diagnostic mode setting.
    /// </summary>
    static DiagnosticMode _globalDebugMode = DiagnosticMode.NONE;

    /// <summary>
    /// Gets the current global diagnostic mode.
    /// </summary>
    /// <returns>
    /// The currently active diagnostic mode flags that determine what information is logged.
    /// </returns>
    /// <remarks>
    /// The diagnostic mode affects all dispatcher instances and controls the level of
    /// debugging information generated during event processing.
    /// </remarks>
    public static DiagnosticMode GetDebugMode() {
        return _globalDebugMode;
    }

    /// <summary>
    /// Checks if a specific diagnostic flag is enabled in the given mode.
    /// </summary>
    /// <param name="mode">The diagnostic mode to check.</param>
    /// <param name="flag">The specific flag to test for.</param>
    /// <returns>
    /// <c>true</c> if the specified flag is set in the mode; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="LSArgumentNullException">
    /// Thrown when either <paramref name="mode"/> or <paramref name="flag"/> is null.
    /// </exception>
    /// <remarks>
    /// This utility method helps determine if specific diagnostic features are enabled
    /// when diagnostic modes are combined using bitwise operations.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check if verbose mode is enabled
    /// if (LSDispatcher.HasFlag(currentMode, DiagnosticMode.VERBOSE)) {
    ///     Console.WriteLine("Verbose logging is enabled");
    /// }
    /// </code>
    /// </example>
    public static bool HasFlag(DiagnosticMode? mode, DiagnosticMode? flag) {
        if (!mode.HasValue || !flag.HasValue) {
            throw new LSArgumentNullException("mode_or_flag_null");
        }
        return (mode & flag) == flag;
    }

    /// <summary>
    /// Sets the global diagnostic mode for all dispatcher instances.
    /// </summary>
    /// <param name="mode">
    /// The diagnostic mode to enable. Can be a combination of flags to enable multiple features.
    /// </param>
    /// <remarks>
    /// Changing the diagnostic mode affects all subsequent event processing across all
    /// dispatcher instances. This is a global setting that impacts the entire event system.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Enable both warnings and errors
    /// LSDispatcher.SetDebugMode(DiagnosticMode.WARNINGS | DiagnosticMode.ERRORS);
    /// 
    /// // Enable all diagnostic features
    /// LSDispatcher.SetDebugMode(DiagnosticMode.VERBOSE | DiagnosticMode.WARNINGS | DiagnosticMode.ERRORS);
    /// </code>
    /// </example>
    public static void SetDebugMode(DiagnosticMode mode) {
        _globalDebugMode = mode;
    }

    /// <summary>
    /// Resets the global diagnostic mode to disable all diagnostic features.
    /// </summary>
    /// <remarks>
    /// This method turns off all diagnostic logging and monitoring features, returning
    /// the system to normal operation mode. This is useful for production deployments
    /// or when diagnostic information is no longer needed.
    /// </remarks>
    public static void ResetDebugMode() {
        _globalDebugMode = DiagnosticMode.NONE;
    }
    #endregion
}
