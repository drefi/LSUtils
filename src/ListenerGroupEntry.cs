using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils;

/// <summary>
/// Represents a group of event listeners organized by event type and grouping strategy.
/// This class manages collections of listeners with various grouping behaviors including
/// static, single-instance, multi-instance, subset, and superset matching.
/// </summary>
/// <remarks>
/// <para>
/// ListenerGroupEntry provides a flexible way to organize and manage event listeners
/// based on different criteria. It supports various group types that determine how
/// listeners are matched and organized:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="ListenerGroupType.STATIC"/>: Global listeners not tied to specific instances</description></item>
/// <item><description><see cref="ListenerGroupType.SINGLE"/>: Listeners tied to a single instance</description></item>
/// <item><description><see cref="ListenerGroupType.MULTI"/>: Listeners tied to multiple specific instances</description></item>
/// <item><description><see cref="ListenerGroupType.SUBSET"/>: Listeners that match any subset of instances</description></item>
/// <item><description><see cref="ListenerGroupType.SUPERSET"/>: Listeners that match any superset of instances</description></item>
/// </list>
/// <para>
/// The class uses thread-safe collections to ensure concurrent access safety and
/// integrates with the broader LSUtils event system.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create a static group for global listeners
/// var staticGroup = ListenerGroupEntry.Create&lt;MyEvent&gt;();
/// 
/// // Create a single-instance group
/// var singleGroup = ListenerGroupEntry.Create&lt;MyEvent&gt;(
///     ListenerGroupType.SINGLE, 
///     new ILSEventable[] { myInstance }
/// );
/// 
/// // Add a listener to the group
/// staticGroup.AddListener(Guid.NewGuid(), (id, evt) => HandleEvent(evt));
/// </code>
/// </example>
public class ListenerGroupEntry {
    #region Static Factory Methods
    /// <summary>
    /// Creates a new <see cref="ListenerGroupEntry"/> with the specified event type and configuration.
    /// </summary>
    /// <param name="eventType">The type of events this group will handle.</param>
    /// <param name="groupType">
    /// The grouping strategy to use. Defaults to <see cref="ListenerGroupType.STATIC"/>.
    /// </param>
    /// <param name="instances">
    /// The instances to associate with this group. Can be null for static groups.
    /// </param>
    /// <returns>A new <see cref="ListenerGroupEntry"/> configured with the specified parameters.</returns>
    /// <remarks>
    /// This factory method provides the most flexible way to create listener groups
    /// when you have a runtime <see cref="LSEventType"/> instance.
    /// </remarks>
    public static ListenerGroupEntry Create(LSEventType eventType, ListenerGroupType groupType = ListenerGroupType.STATIC, ILSEventable[]? instances = null) {
        return new ListenerGroupEntry(eventType, groupType, instances);
    }

    /// <summary>
    /// Creates a new <see cref="ListenerGroupEntry"/> using the given type parameter, group type and instances.
    /// </summary>
    /// <typeparam name="TEvent">The type of event this group will handle. Must inherit from <see cref="LSEvent"/>.</typeparam>
    /// <param name="groupType">The type of group to create. Defaults to <see cref="ListenerGroupType.STATIC"/>.</param>
    /// <param name="instances">The instances to include in the group. Defaults to an empty array.</param>
    /// <returns>A new <see cref="ListenerGroupEntry"/> configured for the specified event type.</returns>
    /// <remarks>
    /// This is the preferred factory method when you know the event type at compile time.
    /// It automatically resolves the <see cref="LSEventType"/> from the generic parameter.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create a static group for MyEvent
    /// var group = ListenerGroupEntry.Create&lt;MyEvent&gt;();
    /// 
    /// // Create a single-instance group
    /// var instanceGroup = ListenerGroupEntry.Create&lt;MyEvent&gt;(
    ///     ListenerGroupType.SINGLE, 
    ///     new[] { myEventableInstance }
    /// );
    /// </code>
    /// </example>
    public static ListenerGroupEntry Create<TEvent>(ListenerGroupType groupType = ListenerGroupType.STATIC, ILSEventable[]? instances = null) where TEvent : LSEvent {
        return new ListenerGroupEntry(LSEventType.Get<TEvent>(), groupType, instances);
    }

    /// <summary>
    /// Determines the appropriate group type based on the provided instances array.
    /// </summary>
    /// <param name="instances">The instances to analyze. Can be null or empty.</param>
    /// <returns>
    /// <see cref="ListenerGroupType.STATIC"/> if instances is null or empty,
    /// <see cref="ListenerGroupType.SINGLE"/> if instances contains one element,
    /// <see cref="ListenerGroupType.MULTI"/> if instances contains multiple elements.
    /// </returns>
    /// <remarks>
    /// This utility method helps automatically determine the most appropriate group type
    /// based on the number of instances provided.
    /// </remarks>
    public static ListenerGroupType GetGroupType(ILSEventable[]? instances = null) {
        return instances == null || instances.Length == 0 ? ListenerGroupType.STATIC : instances.Length == 1 ? ListenerGroupType.SINGLE : ListenerGroupType.MULTI;
    }
    #endregion

    #region Fields and Properties
    /// <summary>
    /// Thread-safe collection of listeners keyed by their unique identifiers.
    /// </summary>
    private readonly ConcurrentDictionary<System.Guid, ListenerEntry<LSEvent>> _listeners = new ConcurrentDictionary<System.Guid, ListenerEntry<LSEvent>>();
    
    /// <summary>
    /// The instances associated with this listener group.
    /// </summary>
    private readonly ILSEventable[] _instances;

    /// <summary>
    /// Gets the class name for debugging and logging purposes.
    /// </summary>
    /// <value>The name of this class.</value>
    public string ClassName => nameof(ListenerGroupEntry);

    /// <summary>
    /// Gets the type of events this group handles.
    /// </summary>
    /// <value>The <see cref="LSEventType"/> that defines what events this group processes.</value>
    /// <remarks>
    /// This property is set during construction and cannot be changed.
    /// It determines which events will be routed to this group's listeners.
    /// </remarks>
    public LSEventType EventType { get; }

    /// <summary>
    /// Gets the grouping strategy used by this listener group.
    /// </summary>
    /// <value>The <see cref="ListenerGroupType"/> that defines how this group matches instances.</value>
    /// <remarks>
    /// This property determines how this group will match against other groups
    /// and how listeners are organized within the group.
    /// </remarks>
    public ListenerGroupType GroupType { get; }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="ListenerGroupEntry"/> class.
    /// </summary>
    /// <param name="eventType">The type of events this group will handle.</param>
    /// <param name="groupType">The grouping strategy to use for this group.</param>
    /// <param name="instances">
    /// The instances to associate with this group. Can be null for static groups.
    /// </param>
    /// <remarks>
    /// This constructor is protected to enforce the use of factory methods for creation.
    /// If instances is null, an empty array is used internally.
    /// </remarks>
    protected ListenerGroupEntry(LSEventType eventType, ListenerGroupType groupType, ILSEventable[]? instances) {
        EventType = eventType;
        _instances = instances == null ? new ILSEventable[0] : instances;
        GroupType = groupType;
    }
    #endregion

    #region Instance Management
    /// <summary>
    /// Gets a copy of the instances associated with this listener group.
    /// </summary>
    /// <returns>An array containing all instances associated with this group.</returns>
    /// <remarks>
    /// This method returns a copy of the internal instances array to prevent
    /// external modification of the group's instance collection.
    /// </remarks>
    public ILSEventable[] GetInstances() {
        return _instances;
    }
    #endregion

    #region Group Matching and Comparison
    /// <summary>
    /// Determines whether this listener group contains or matches the specified group
    /// based on their respective group types and instances.
    /// </summary>
    /// <param name="other">The other listener group to compare against.</param>
    /// <returns>
    /// <c>true</c> if this group contains or matches the other group according to
    /// the matching rules defined by the other group's <see cref="GroupType"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method implements complex matching logic based on the <paramref name="other"/> group's type:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="ListenerGroupType.STATIC"/>: Matches only if both groups are static</description></item>
    /// <item><description><see cref="ListenerGroupType.SINGLE"/>: Matches if this group has one instance that exists in the other group</description></item>
    /// <item><description><see cref="ListenerGroupType.MULTI"/>: Matches if both are multi-type and instances align in chunks</description></item>
    /// <item><description><see cref="ListenerGroupType.SUBSET"/>: Matches if this group's instances are a subset of the other group's</description></item>
    /// <item><description><see cref="ListenerGroupType.SUPERSET"/>: Matches if this group's instances are a superset of the other group's</description></item>
    /// </list>
    /// <para>
    /// Groups with <see cref="ListenerGroupType.INVALID"/> will never match.
    /// Both groups must handle the same <see cref="EventType"/> to match.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check if a static group contains another static group
    /// var staticGroup1 = ListenerGroupEntry.Create&lt;MyEvent&gt;();
    /// var staticGroup2 = ListenerGroupEntry.Create&lt;MyEvent&gt;();
    /// bool matches = staticGroup1.Contains(staticGroup2); // true
    /// 
    /// // Check subset matching
    /// var largeGroup = ListenerGroupEntry.Create&lt;MyEvent&gt;(
    ///     ListenerGroupType.MULTI, new[] { instanceA, instanceB, instanceC }
    /// );
    /// var subsetGroup = ListenerGroupEntry.Create&lt;MyEvent&gt;(
    ///     ListenerGroupType.SUBSET, new[] { instanceA, instanceB }
    /// );
    /// bool isSubset = largeGroup.Contains(subsetGroup); // true
    /// </code>
    /// </example>
    public bool Contains(ListenerGroupEntry other) {
        if (GroupType == ListenerGroupType.INVALID ||
            other.GroupType == ListenerGroupType.INVALID ||
            EventType != other.EventType)
            return false;
        var instances = GetInstances();
        var otherInstances = other.GetInstances();

        return other.GroupType switch {
            //Will only be true if this GroupType also is static (meaning I'm looking for the static group)
            ListenerGroupType.STATIC => GroupType == ListenerGroupType.STATIC,
            //Looking for groups with single instances, otherInstances may have more than one searching instance, in this case if any instance match in this instances it's true
            ListenerGroupType.SINGLE => (instances.Length == 1 && otherInstances.Any(instance => instances.Contains(instance))),
            //Looking for groups with multiple keys, this requires that this GroupType is also MULTI, otherInstances must be multiple of instances, for each chunk in otherInstances, check if the chunk is equal to instances
            ListenerGroupType.MULTI => (GroupType == ListenerGroupType.MULTI && otherInstances.Length % instances.Length == 0 && otherInstances.Chunk(instances.Length).All(chunk => chunk.ToHashSet().SetEquals(instances.ToHashSet()))),
            //Search for a subset of otherInstances (e.g: otherInstances[A,B,C] == [A,B,C] || [A,B] || [A] / otherInstances[A,B] == [A,B] || [A]) 
            // NOTE: STATIC is always a subset, SUBSET cannot be used with multipe chunks
            ListenerGroupType.SUBSET => instances.ToHashSet().IsSubsetOf(otherInstances.ToHashSet()),
            //Search for superset of otherInstances (e.g: otherInstances[A] == [A,B,C] || [A,B] || [A] / otherInstances[A,B] == [A,B,C] || [A,B])
            ListenerGroupType.SUPERSET => instances.ToHashSet().IsSupersetOf(otherInstances.ToHashSet()),
            _ => false,
        };

    }
    #endregion

    #region Listener Management
    /// <summary>
    /// Adds a new listener to this group with the specified configuration.
    /// </summary>
    /// <param name="listenerID">
    /// The unique identifier for the listener. If <see cref="System.Guid.Empty"/> or default,
    /// a new GUID will be automatically generated.
    /// </param>
    /// <param name="listener">The callback function to execute when events are triggered.</param>
    /// <param name="triggers">
    /// The maximum number of times this listener can be executed.
    /// Use -1 for unlimited triggers. Defaults to -1.
    /// </param>
    /// <returns><c>true</c> if the listener was successfully added; otherwise, <c>false</c>.</returns>
    /// <exception cref="LSException">
    /// Thrown when the listener ID already exists in the group or when the listener
    /// could not be added to the internal collection.
    /// </exception>
    /// <remarks>
    /// This method is thread-safe and uses atomic operations to ensure the listener
    /// is added correctly even under concurrent access.
    /// </remarks>
    /// <example>
    /// <code>
    /// var group = ListenerGroupEntry.Create&lt;MyEvent&gt;();
    /// 
    /// // Add an unlimited listener
    /// group.AddListener(Guid.NewGuid(), (id, evt) => HandleEvent(evt));
    /// 
    /// // Add a one-time listener
    /// group.AddListener(Guid.NewGuid(), (id, evt) => HandleEventOnce(evt), 1);
    /// </code>
    /// </example>
    public bool AddListener(System.Guid listenerID, LSListener<LSEvent> listener, int triggers = -1) {
        if (listenerID == default || listenerID == System.Guid.Empty) listenerID = System.Guid.NewGuid();
        if (_listeners.ContainsKey(listenerID)) {
            throw new LSException($"listener_id_already_added");
        }
        if (_listeners.TryAdd(listenerID, new ListenerEntry<LSEvent>(listenerID, listener, triggers)) == false) {
            throw new LSException($"listener_id_could_not_be_added");
        }
        return true;
    }

    // public bool AddListener<TEvent>(System.Guid listenerID, LSListener<TEvent> listener, int triggers = -1) where TEvent : LSEvent {
    //     if (listenerID == default || listenerID == System.Guid.Empty) listenerID = System.Guid.NewGuid();
    //     if (_listeners.ContainsKey(listenerID)) {
    //         throw new LSException($"listener_id_already_added");
    //     }
    //     if (_listeners.TryAdd(listenerID, new ListenerEntry<TEvent>(listenerID, listener, triggers)) == false) {
    //         throw new LSException($"listener_id_could_not_be_added");
    //     }
    //     return true;
    // }

    /// <summary>
    /// Removes a listener from this group by its unique identifier.
    /// </summary>
    /// <param name="listenerID">The unique identifier of the listener to remove.</param>
    /// <returns><c>true</c> if the listener was successfully removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="LSException">
    /// Thrown when the listener with the specified ID could not be found or removed.
    /// </exception>
    /// <remarks>
    /// This method is thread-safe and uses atomic operations to ensure the listener
    /// is removed correctly even under concurrent access.
    /// </remarks>
    public bool RemoveListener(System.Guid listenerID) {
        string log = $"{ClassName}::UnregisterListener";
        if (_listeners.TryRemove(listenerID, out _) == false) {
            throw new LSException($"{listenerID}_could_not_be_removed");
        }
        return true;
    }

    /// <summary>
    /// Gets an array of all listener IDs currently registered with this group.
    /// </summary>
    /// <returns>An array containing the unique identifiers of all registered listeners.</returns>
    /// <remarks>
    /// This method returns a snapshot of the listener IDs at the time of the call.
    /// The actual collection may change due to concurrent operations.
    /// </remarks>
    public System.Guid[] GetListeners() {
        return _listeners.Keys.ToArray();
    }

    /// <summary>
    /// Gets the current number of listeners registered with this group.
    /// </summary>
    /// <returns>The count of registered listeners.</returns>
    /// <remarks>
    /// This property provides a thread-safe way to check the current listener count
    /// without having to retrieve all listener IDs.
    /// </remarks>
    public int GetListenersCount() => _listeners.Count;
    #endregion

    #region Event Notification
    /// <summary>
    /// Notifies all listeners in this group about the specified event.
    /// </summary>
    /// <param name="event">The event to broadcast to all listeners.</param>
    /// <returns>
    /// <c>true</c> if all listeners were successfully notified and the event was not cancelled;
    /// <c>false</c> if the event was cancelled or marked as done during processing.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method iterates through all registered listeners and executes their callbacks.
    /// Listeners that have exhausted their trigger count (return 0 from Execute) are
    /// automatically removed from the group.
    /// </para>
    /// <para>
    /// Processing stops early if the event is cancelled (<see cref="LSEvent.IsCancelled"/>)
    /// or marked as done (<see cref="LSEvent.IsDone"/>) by any listener.
    /// </para>
    /// <para>
    /// This method is thread-safe regarding listener collection modifications that might
    /// occur during iteration.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var group = ListenerGroupEntry.Create&lt;MyEvent&gt;();
    /// group.AddListener(Guid.NewGuid(), (id, evt) => Console.WriteLine("Event received"));
    /// 
    /// var myEvent = new MyEvent();
    /// bool completed = group.NotifyListeners(myEvent);
    /// </code>
    /// </example>
    public bool NotifyListeners(LSEvent @event) {
        foreach (var listener in _listeners) {
            var count = listener.Value.Execute(@event);
            if (count == 0) _listeners.TryRemove(listener.Key, out _);
            if (@event.IsCancelled || @event.IsDone) return false;
        }
        return true;
    }
    #endregion

    #region Object Overrides and Operators
    /// <summary>
    /// Determines whether the specified object is equal to this listener group.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the specified object is a <see cref="ListenerGroupEntry"/> with the same
    /// <see cref="EventType"/>, <see cref="GroupType"/>, and instances; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Two listener groups are considered equal if they have the same event type,
    /// group type, and identical instances in the same order.
    /// </remarks>
    public override bool Equals(object? obj) {
        if (obj is not ListenerGroupEntry other || EventType != other.EventType || GroupType != other.GroupType) return false;
        ILSEventable[] instances = GetInstances();
        ILSEventable[] otherInstances = other.GetInstances();
        if (instances.Length != otherInstances.Length) return false;
        for (var i = 0; i < instances.Length; i++) if (!instances[i].Equals(otherInstances[i])) return false;
        return true;
    }

    /// <summary>
    /// Returns a hash code for this listener group.
    /// </summary>
    /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures.</returns>
    /// <remarks>
    /// The hash code is computed based on the <see cref="EventType"/>, <see cref="GroupType"/>,
    /// and all associated instances to ensure consistent behavior with <see cref="Equals(object)"/>.
    /// </remarks>
    public override int GetHashCode() {
        unchecked {
            var hashCode = new System.HashCode();
            hashCode.Add(this.EventType);
            hashCode.Add(this.GroupType);
            foreach (var item in this._instances) {
                hashCode.Add(item);
            }
            return hashCode.ToHashCode();
        }
    }

    /// <summary>
    /// Determines whether two <see cref="ListenerGroupEntry"/> instances are equal.
    /// </summary>
    /// <param name="left">The first listener group to compare.</param>
    /// <param name="right">The second listener group to compare.</param>
    /// <returns><c>true</c> if the listener groups are equal; otherwise, <c>false</c>.</returns>
    /// <exception cref="LSException">
    /// Thrown if either listener group has an <see cref="ListenerGroupType.INVALID"/> group type.
    /// </exception>
    /// <remarks>
    /// This operator provides a convenient way to compare listener groups and includes
    /// validation to prevent comparison of invalid groups.
    /// </remarks>
    public static bool operator ==(ListenerGroupEntry left, ListenerGroupEntry right) {
        if (left?.GroupType == ListenerGroupType.INVALID || right?.GroupType == ListenerGroupType.INVALID) {
            throw new LSException("ls_event_type_invalid");
        }
        if (ReferenceEquals(left, right) || (left is null && right is null)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="ListenerGroupEntry"/> instances are not equal.
    /// </summary>
    /// <param name="a">The first listener group to compare.</param>
    /// <param name="b">The second listener group to compare.</param>
    /// <returns><c>true</c> if the listener groups are not equal; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This operator is the logical inverse of the equality operator.
    /// </remarks>
    public static bool operator !=(ListenerGroupEntry a, ListenerGroupEntry b) {
        return !(a == b);
    }
    #endregion
}


/// <summary>
/// Defines the different strategies for grouping and matching event listeners.
/// </summary>
/// <remarks>
/// <para>
/// The listener group type determines how listener groups are organized and how they
/// match against other groups during event dispatching. Different types provide
/// various levels of instance-based filtering and organization.
/// </para>
/// <para>
/// Example use cases with groups objA=[A,B,C], objB=[A,B], objC=[D,B], objD=[D,B,C], objE=[A,B,E], objF=[A]:
/// </para>
/// <list type="bullet">
/// <item><description>search(SUBSET)=[A,B,C] matches: objA, objB, objF</description></item>
/// <item><description>search(SUPERSET)=[A,B] matches: objA, objB, objE</description></item>
/// </list>
/// </remarks>
public enum ListenerGroupType {
    /// <summary>
    /// Represents a static group not tied to any specific instances.
    /// These groups handle global event listeners that apply to all events of their type.
    /// </summary>
    /// <remarks>
    /// Static groups are the most common type and are used for application-wide event handling.
    /// They match only with other static groups of the same event type.
    /// </remarks>
    STATIC,

    /// <summary>
    /// Represents a group tied to a single specific instance.
    /// These groups handle events that are relevant only to one particular object.
    /// </summary>
    /// <remarks>
    /// Single-instance groups are useful for object-specific event handling where
    /// listeners should only respond to events from a particular source.
    /// </remarks>
    SINGLE,

    /// <summary>
    /// Represents a group tied to multiple specific instances.
    /// These groups handle events that are relevant to a specific set of objects.
    /// </summary>
    /// <remarks>
    /// Multi-instance groups enable complex event routing where listeners are
    /// interested in events from a specific combination of sources.
    /// </remarks>
    MULTI,

    /// <summary>
    /// Represents a subset matching strategy (used in search operations).
    /// Groups with this type match if their instances are a subset of the compared group's instances.
    /// </summary>
    /// <remarks>
    /// This type is primarily used in search and filtering operations to find groups
    /// that contain at least the specified instances, possibly with additional ones.
    /// </remarks>
    SUBSET,

    /// <summary>
    /// Represents a superset matching strategy (used in search operations).
    /// Groups with this type match if their instances are a superset of the compared group's instances.
    /// </summary>
    /// <remarks>
    /// This type is used to find groups that contain all specified instances and potentially fewer,
    /// useful for finding more general event handlers.
    /// </remarks>
    SUPERSET,

    /// <summary>
    /// Represents an invalid or uninitialized group type.
    /// Groups with this type should not be used and will cause exceptions in operations.
    /// </summary>
    /// <remarks>
    /// This value is used to indicate error states or uninitialized groups.
    /// Any operation involving INVALID groups will typically throw exceptions.
    /// </remarks>
    INVALID
}
