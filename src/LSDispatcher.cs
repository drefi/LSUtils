using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils;

/// <summary>
/// Responsible for managing and dispatching events to registered listeners.
/// </summary>
public partial class LSDispatcher {
    #region Static
    /// <summary>
    /// Event triggered for delay callbacks.
    /// </summary>

    static LSDispatcher? _instance;
    private static readonly object _lockObj = new object();

    /// <summary>
    /// Gets the singleton instance of the <see cref="LSDispatcher"/>.
    /// </summary>
    public static LSDispatcher Instance {
        get {
            lock (_lockObj) {
                if (_instance == null) _instance = new LSDispatcher();
                return _instance;
            }
        }
    }

    #endregion    
    /// <summary>
    /// Stores the listeners registered for different events, identified by a hash code.
    /// </summary>
    readonly ConcurrentDictionary<int, ListenerGroupEntry> _listeners = new ConcurrentDictionary<int, ListenerGroupEntry>();

    /// <summary>
    /// The class name of the <see cref="LSDispatcher"/> class.
    /// </summary>
    public string ClassName => nameof(LSDispatcher);
    public LSAction<float, LSAction> Delay { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSDispatcher"/> class.
    /// </summary>
    protected LSDispatcher() {
        Delay = (delay, callback) => callback?.Invoke();
    }
    public LSDispatcher(LSAction<float, LSAction> delayHandler) {
        Delay = delayHandler;
    }
    public void SetDelayHandler(LSAction<float, LSAction> delayHandler) {
        Delay = delayHandler;
    }

    internal bool Dispatch(ListenerGroupEntry searchGroup, LSEvent @event) {
        if (@event == null) throw new LSException("{{event_null}}");
        if (@event.IsCancelled || @event.IsDone) throw new LSException($"{{event{(@event.IsCancelled ? "_cancelled" : "_done")}}}");
        LSEventType eventType = LSEventType.Get(@event.GetType());
        var matches = _listeners.Where(entry => entry.Value.Contains(searchGroup));
        foreach (var match in matches) {
            if (match.Value.NotifyListeners(@event) == false) return false;
        }
        return @event.Signal(out _) == 0;
    }

    /// <summary>
    /// Registers a listener for the given event type and instance set.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to listen for.</typeparam>
    /// <param name="listener">The listener to register.</param>
    /// <param name="instances">The instances associated with the listener. Defaults to an empty array.</param>
    /// <param name="triggers">The number of times the listener should be invoked. Defaults to -1 (infinite).</param>
    /// <param name="listenerID">The identifier of the listener to register. Defaults to a new GUID.</param>
    /// <param name="errorHandler">An optional callback to invoke if any errors occur during registration.</param>
    /// <returns>The identifier of the registered listener.</returns>
    /// <exception cref="LSArgumentNullException">Thrown if the listener is null.</exception>
    /// <exception cref="LSException">Thrown if the event type is invalid, or if any errors occur during registration.</exception>
    public System.Guid Register(LSListener<LSEvent> listener, LSEventType eventType, ILSEventable[]? instances = null, int triggers = -1, System.Guid listenerID = default, LSMessageHandler? errorHandler = null) {
        if (listener == null) {
            errorHandler?.Invoke("{{listener_null}}");
            return System.Guid.Empty;
        }
        ListenerGroupType groupType = ListenerGroupEntry.GetGroupType(instances);
        ListenerGroupEntry group = ListenerGroupEntry.Create(eventType);
        if (_listeners.TryGetValue(group.GetHashCode(), out var existingGroup) == false) {
            if (_listeners.TryAdd(group.GetHashCode(), group) == false) throw new LSException($"{{add_new_group_failed_{groupType}}}:{group.GetHashCode()}");
        } else group = existingGroup;
        if (listenerID == default || listenerID == System.Guid.Empty) listenerID = System.Guid.NewGuid();
        if (group.AddListener(listenerID, listener, triggers) == false) throw new LSException($"{{group_{group.GroupType}_failed_register_listener}}:{listenerID}");
        return listenerID;

    }
    public System.Guid Register<TEvent>(LSListener<TEvent> listener, ILSEventable[]? instances = null, int triggers = -1, System.Guid listenerID = default, LSMessageHandler? errorHandler = null) where TEvent : LSEvent {
        return Register(
            new LSListener<LSEvent>((id, e) => listener(id, (TEvent)(object)e)),
            LSEventType.Get<TEvent>(),
            instances,
            triggers,
            listenerID,
            errorHandler
        );
    }

    public bool Unregister(System.Guid listenerID) {
        var match = _listeners.Where(g => g.Value.GetListeners().Contains(listenerID));
        return match.Any(entry => entry.Value.RemoveListener(listenerID));
    }

    public bool Unregister<TEvent>(ILSEventable[]? instances = null) where TEvent : LSEvent {
        LSEventType eventType = LSEventType.Get<TEvent>();
        ListenerGroupType groupType = ListenerGroupEntry.GetGroupType(instances);
        ListenerGroupEntry search = ListenerGroupEntry.Create(eventType, groupType, instances);
        var match = _listeners.Where(entry => entry.Value.Contains(search));
        if (match.Count() == 0) throw new LSException($"no_listeners_for_{typeof(TEvent)}");
        return match.Any(entry => _listeners.TryRemove(entry.Key, out _));
    }
    public int GetListenersCount(LSEventType eventType, ListenerGroupType groupType, ILSEventable[]? instances = null) {
        var searchGroup = ListenerGroupEntry.Create(eventType, groupType, instances);
        return _listeners
            .AsParallel()
            .Where(entry => entry.Value.Contains(searchGroup))
            .Sum(listener => listener.Value.GetListenersCount());
    }
    /// <summary>
    /// Gets the number of listeners registered for the given event type and instance set.
    /// </summary>
    /// <param name="eventType">The type of event to get the listener count for.</param>
    /// <param name="groupType">The type of group to search for. Defaults to <see cref="ListenerGroupType.STATIC"/>.</param>
    /// <param name="instances">The instances associated with the listener. Defaults to an empty array.</param>
    /// <returns>The number of listeners registered for the given event type and instance set.</returns>
    /// <exception cref="LSArgumentNullException">Thrown if the event type is null.</exception>
    public int GetListenersCount<TEvent>(ListenerGroupType groupType, ILSEventable[]? instances = null) where TEvent : LSEvent {
        LSEventType eventType = LSEventType.Get(typeof(TEvent));
        return GetListenersCount(eventType, groupType, instances);
    }


    #region DiagnosticMode

    /// <summary>
    /// Defines diagnostic modes for debugging.
    /// </summary>
    [System.Flags]
    public enum DiagnosticMode {
        NONE = 0,
        VERBOSE = 1,
        WARNINGS = 2,
        ERRORS = 4
    }

    static DiagnosticMode _globalDebugMode = DiagnosticMode.NONE;

    /// <summary>
    /// Gets the current global debug mode.
    /// </summary>
    /// <returns>The current debug mode.</returns>
    public static DiagnosticMode GetDebugMode() {
        return _globalDebugMode;
    }

    /// <summary>
    /// Checks if a specific flag is set in the mode.
    /// </summary>
    /// <param name="mode">The mode to check.</param>
    /// <param name="flag">The flag to look for.</param>
    /// <returns>True if the flag is set, otherwise false.</returns>
    /// <exception cref="LSArgumentNullException">Thrown if mode or flag is null.</exception>
    public static bool HasFlag(DiagnosticMode? mode, DiagnosticMode? flag) {
        if (!mode.HasValue || !flag.HasValue) {
            throw new LSArgumentNullException("mode_or_flag_null");
        }
        return (mode & flag) == flag;
    }

    /// <summary>
    /// Sets the global debug mode.
    /// </summary>
    /// <param name="mode">The mode to set.</param>
    public static void SetDebugMode(DiagnosticMode mode) {
        _globalDebugMode = mode;
    }

    /// <summary>
    /// Resets the global debug mode to NONE.
    /// </summary>
    public static void ResetDebugMode() {
        _globalDebugMode = DiagnosticMode.NONE;
    }
    #endregion
}
