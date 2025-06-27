using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils;

public class ListenerGroupEntry {
    #region Static
    public static ListenerGroupEntry Create(LSEventType eventType, ListenerGroupType groupType = ListenerGroupType.STATIC, ILSEventable[]? instances = null) {
        return new ListenerGroupEntry(eventType, groupType, instances);
    }
    /// <summary>
    /// Creates a new <see cref="ListenerGroupEntry"/> using the given type parameter, group type and instances.
    /// </summary>
    /// <typeparam name="TEvent">The type of event.</typeparam>
    /// <param name="groupType">The type of group to create. Defaults to <see cref="ListenerGroupType.STATIC"/>.</param>
    /// <param name="instances">The instances to include in the group. Defaults to an empty array.</param>
    /// <returns>A new <see cref="ListenerGroupEntry"/>.</returns>
    public static ListenerGroupEntry Create<TEvent>(ListenerGroupType groupType = ListenerGroupType.STATIC, ILSEventable[]? instances = null) where TEvent : LSEvent {
        return new ListenerGroupEntry(LSEventType.Get<TEvent>(), groupType, instances);
    }
    #endregion
    #region Fields
    private readonly ConcurrentDictionary<System.Guid, ListenerEntry<LSEvent>> _listeners = new ConcurrentDictionary<System.Guid, ListenerEntry<LSEvent>>();
    private readonly ILSEventable[] _instances;
    public string ClassName => nameof(ListenerGroupEntry);
    public LSEventType EventType { get; }
    public ListenerGroupType GroupType { get; }
    #endregion

    protected ListenerGroupEntry(LSEventType eventType, ListenerGroupType groupType, ILSEventable[]? instances) {
        EventType = eventType;
        _instances = instances == null ? new ILSEventable[0] : instances;
        GroupType = groupType;
    }

    #region Public Methods
    public ILSEventable[] GetInstances() {
        return _instances;
    }
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
            ListenerGroupType.SINGLE => GroupType == ListenerGroupType.STATIC || (instances.Length == 1 && otherInstances.Any(instance => instances.Contains(instance))),
            //Looking for groups with multiple keys, this requires that this GroupType is also MULTI, otherInstances must be multiple of instances, for each chunk in otherInstances, check if the chunk is equal to instances
            ListenerGroupType.MULTI => GroupType == ListenerGroupType.STATIC || (GroupType == ListenerGroupType.MULTI && otherInstances.Length % instances.Length == 0 && otherInstances.Chunk(instances.Length).All(chunk => chunk.ToHashSet().SetEquals(instances.ToHashSet()))),
            //Search for a subset of otherInstances (e.g: otherInstances[A,B,C] == [A,B,C] || [A,B] || [A] / otherInstances[A,B] == [A,B] || [A]) 
            // NOTE: STATIC is always a subset, SUBSET cannot be used with multipe chunks
            ListenerGroupType.SUBSET => instances.ToHashSet().IsSubsetOf(otherInstances.ToHashSet()),
            //Search for superset of otherInstances (e.g: otherInstances[A] == [A,B,C] || [A,B] || [A] / otherInstances[A,B] == [A,B,C] || [A,B])
            ListenerGroupType.SUPERSET => instances.ToHashSet().IsSupersetOf(otherInstances.ToHashSet()),
            _ => false,
        };

    }
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
    public bool RemoveListener(System.Guid listenerID) {
        string log = $"{ClassName}::UnregisterListener";
        if (_listeners.TryRemove(listenerID, out _) == false) {
            throw new LSException($"{listenerID}_could_not_be_removed");
        }
        return true;
    }
    public System.Guid[] GetListeners() {
        return _listeners.Keys.ToArray();
    }

    public bool NotifyListeners(LSEvent @event) {
        foreach (var listener in _listeners) {
            var count = listener.Value.Execute(@event);
            if (count == 0) _listeners.TryRemove(listener.Key, out _);
            if (@event.IsCancelled || @event.IsDone) return false;
        }
        return true;
    }
    public int GetListenersCount() => _listeners.Count;
    #endregion

    public override bool Equals(object? obj) {
        if (obj is not ListenerGroupEntry other || EventType != other.EventType || GroupType != other.GroupType) return false;
        ILSEventable[] instances = GetInstances();
        ILSEventable[] otherInstances = other.GetInstances();
        if (instances.Length != otherInstances.Length) return false;
        for (var i = 0; i < instances.Length; i++) if (!instances[i].Equals(otherInstances[i])) return false;
        return true;
    }
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
    public static bool operator ==(ListenerGroupEntry left, ListenerGroupEntry right) {
        if (left?.GroupType == ListenerGroupType.INVALID || right?.GroupType == ListenerGroupType.INVALID) {
            throw new LSException("ls_event_type_invalid");
        }
        if (ReferenceEquals(left, right) || (left is null && right is null)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }
    public static bool operator !=(ListenerGroupEntry a, ListenerGroupEntry b) {
        return !(a == b);
    }
    public static ListenerGroupType GetGroupType(ILSEventable[]? instances = null) {
        return instances == null || instances.Length == 0 ? ListenerGroupType.STATIC : instances.Length == 1 ? ListenerGroupType.SINGLE : ListenerGroupType.MULTI;
    }
}


public enum ListenerGroupType {
    //use cases:
    // objA=[A,B,C], objB=[A,B], objC=[D,B], objD=[D,B,C], objE=[A,B,E], objF=[A]
    // search(subset)=[A,B,C] => objA.Contains(search)==true; objB.Contains(search)==true; objC.Contains(search)==false); objD.Contains(search)==false; objE.Contains(search)==false; objF.Contains(search)==true;
    // search(superset)=[A,B] => objA.Contains(search)==true; objB.Contains(search)==true; objC.Contains(search)==false); objD.Contains(search)==false; objE.Contains(search)==true; objF.Contains(search)==false;
    STATIC, //select the static group
    SINGLE, //one instance is used as KEY
    MULTI, //multiple instances are used as KEY
    SUBSET, //keys are treated as subset of comparer (only applicable in search)
    SUPERSET,//keys are treated as superset of comparer (only applicable in search)
    INVALID
}
