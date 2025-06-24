using System.Collections.Concurrent;
using System.Linq;

namespace LSUtils;

public class LSEventableType : ILSEventable {
    public System.Type EventableType { get; }
    public virtual string ClassName => nameof(LSEventableType);
    public virtual string Description => $"{EventableType.ToString()}";

    public virtual System.Guid ID { get; }

    protected LSEventableType(System.Type type) {
        EventableType = type;
        ID = System.Guid.NewGuid();
    }

    public bool Initialize(LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        return OnInitializeEvent.Create<LSEventableType>(this, onSuccess, onFailure).Dispatch(onFailure, dispatcher);
    }

    /// <summary>
    /// Gets the <see cref="LSEventableType"/> associated with the given <paramref name="eventableType"/>.
    /// If no matching instance is found, a new one is created and added to the cache.
    /// </summary>
    /// <param name="eventableType">The type of eventable to search for.</param>
    /// <returns>The matching instance, or a newly created one if none was found.</returns>
    public static LSEventableType Get(System.Type eventableType) {
        if (typeof(ILSEventable).IsAssignableFrom(eventableType) == false) throw new LSException($"{eventableType}_is_not_ILSEventable");
        if (_eventTypes.TryGetValue(eventableType, out LSEventableType? instance) == false) {
            instance = new LSEventableType(eventableType);
            _eventTypes.TryAdd(instance.EventableType, instance);

        }
        return instance;
    }

    public void Cleanup() {
        throw new NotImplementedException();
    }

    static readonly ConcurrentDictionary<System.Type, LSEventableType> _eventTypes = new ConcurrentDictionary<System.Type, LSEventableType>();

}
public class LSEventableType<TEventable> : LSEventableType where TEventable : ILSEventable {
    protected LSEventableType() : base(typeof(TEventable)) { }

    public static LSEventableType Get() {
        return LSEventableType.Get(typeof(TEventable));
    }
}
public class LSEventType {
    public System.Type EventType { get; }
    protected LSEventType(System.Type type) {
        EventType = type;
    }
    public static LSEventType Get(System.Type eventType) {
        if (typeof(LSEvent).IsAssignableFrom(eventType) == false) throw new LSException($"{eventType}_is_not_ILSEvent");
        if (_eventTypes.TryGetValue(eventType, out LSEventType? instance) == false) {
            instance = new LSEventType(eventType);
            _eventTypes.TryAdd(instance.EventType, instance);
        }
        return instance;
    }
    public static LSEventType Get<TEvent>() => Get(typeof(TEvent));
    static readonly ConcurrentDictionary<System.Type, LSEventType> _eventTypes = new ConcurrentDictionary<System.Type, LSEventType>();
}
