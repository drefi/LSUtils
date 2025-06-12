namespace LSUtils;

public abstract class LSEvent {
    #region Fields
    protected static LSAction<float, LSAction>? _delayHandler;
    internal static void SetDelayHandler(LSAction<float, LSAction> handler) => _delayHandler = handler;
    private readonly Semaphore _semaphore;
    protected LSDispatcher? _dispatcher;
    public virtual string ClassName => nameof(LSEvent);
    public virtual System.Guid ID { get; }
    public bool HasDispatched { get; protected set; }
    public bool IsDone => _semaphore.IsDone;
    public bool IsCancelled => _semaphore.IsCancelled;
    public bool HasFailed => _semaphore.HasFailed;
    public int Count => _semaphore.Count;
    public virtual ListenerGroupType GroupType { get; protected set; } = ListenerGroupType.STATIC;
    public event LSAction SuccessCallback {
        add { _semaphore.SuccessCallback += value; }
        remove { _semaphore.SuccessCallback -= value; }
    }
    public event LSAction CancelCallback {
        add { _semaphore.CancelCallback += value; }
        remove { _semaphore.CancelCallback -= value; }
    }
    public event LSMessageHandler FailureCallback {
        add { _semaphore.FailureCallback += value; }
        remove { _semaphore.FailureCallback -= value; }
    }
    #endregion
    public LSEvent(System.Guid eventID) {
        _semaphore = Semaphore.Create();
        ID = eventID == default || eventID == System.Guid.Empty ? System.Guid.NewGuid() : eventID;
        GroupType = ListenerGroupType.STATIC;
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEvent"/> class with the given instances and group type.
    /// </summary>
    /// <param name="instances">The instances to associate with this event.</param>
    /// <param name="groupType">The type of group to associate with this event.</param>
    /// <param name="eventID">An optional ID to assign to this event. If not specified, a new GUID is generated.</param>
    public virtual bool Dispatch(LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        _dispatcher = dispatcher ?? LSDispatcher.Instance;
        ListenerGroupEntry searchGroup = ListenerGroupEntry.Create(LSEventType.Get(GetType()), GroupType, GetInstances());
        HasDispatched = true;
        return _dispatcher.Dispatch(searchGroup, this, onFailure);
    }

    public void Wait(float delay = 0f, System.Guid signalID = default, LSAction? delayCallback = null, LSMessageHandler? onFailure = null) {
        _semaphore.Wait(signalID, onFailure);
        if (delay > 0) {
            if (_delayHandler == null) {
                onFailure?.Invoke($"delay_callback_null");
                return;
            }
            LSAction callback = new LSAction(() => {
                _semaphore.Signal(out _, onFailure);
                delayCallback?.Invoke();
            });
            _delayHandler(delay, callback);
        }
    }
    public bool Signal(out System.Guid signalID, LSMessageHandler? onFailure = null) => _semaphore.Signal(out signalID, onFailure);
    public System.Guid[] Cancel(LSMessageHandler? onFailure = null) => _semaphore.Cancel(onFailure);
    public bool Failure(out System.Guid signalID, string msg, LSMessageHandler? onFailure = null) => _semaphore.Failure(out signalID, msg, onFailure);
    public virtual ILSEventable[] GetInstances() => new ILSEventable[0];
    public LSMessageHandler GetFailureCallback() => _semaphore.GetFailureCallback();
    public LSDispatcher GetDispatcher() {
        return _dispatcher == null ? LSDispatcher.Instance : _dispatcher;
    }
}
public abstract class LSEvent<TInstance> : LSEvent where TInstance : ILSEventable {
    private readonly ILSEventable[] _instances;
    /// <summary>
    /// Creates a new <see cref="LSEvent"/> with a primary instance.
    /// The event type is determined by the type parameter <typeparamref name="TInstance"/>.
    /// The event group type is set to <see cref="ListenerGroupType.SUBSET"/>.
    /// </summary>
    /// <param name="instance">The primary instance.</param>
    public TInstance? Instance => _instances.Length > 0 ? (TInstance)_instances[0] : default(TInstance);

    protected LSEvent(System.Guid eventID, ILSEventable[] instances, ListenerGroupType groupType = ListenerGroupType.SUBSET) : base(eventID) {
        GroupType = groupType;
        _instances = instances;
    }
    protected LSEvent(TInstance instance) : this(System.Guid.NewGuid(), new ILSEventable[] { instance }, ListenerGroupType.SINGLE) {
    }
    public override ILSEventable[] GetInstances() {
        return _instances;
    }
}
public abstract class LSEvent<TPrimaryInstance, TSecondaryInstance> : LSEvent<TPrimaryInstance> where TPrimaryInstance : ILSEventable where TSecondaryInstance : ILSEventable {
    /// <summary>
    /// Creates a new <see cref="LSEvent"/> with a primary and secondary instance. The event type is determined
    /// by the type parameter <typeparamref name="TPrimaryInstance"/> and <typeparamref name="TSecondaryInstance"/>. The
    /// event group type is set to <see cref="LSDispatcher.ListenerGroupType.SUBSET"/>.
    /// </summary>
    /// <param name="primaryInstance">The primary instance.</param>
    /// <param name="secondaryInstance">The secondary instance.</param>
    protected LSEvent(TPrimaryInstance primaryInstance, TSecondaryInstance secondaryInstance, ListenerGroupType groupType = ListenerGroupType.SUBSET) : base(System.Guid.NewGuid(), (new ILSEventable[] { primaryInstance, secondaryInstance }).Where(x => x != null).ToArray(), groupType) {

    }
}
public abstract class OnInitializeEvent : LSEvent<ILSEventable> {
    protected OnInitializeEvent(ILSEventable instance) : base(instance) { }

    public static OnInitializeEvent<TInstance> Create<TInstance>(TInstance? instance, LSAction? onSuccess = null, LSMessageHandler? onFailure = null) where TInstance : ILSEventable {
        return OnInitializeEvent<TInstance>.Create(instance, onSuccess, onFailure);
    }
    public static System.Guid Register<TInstance>(LSListener<OnInitializeEvent<TInstance>> listener, ILSEventable[] instances = null!, int triggers = -1, System.Guid listenerID = default, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) where TInstance : ILSEventable {
        dispatcher ??= LSDispatcher.Instance;
        return dispatcher.Register<OnInitializeEvent<TInstance>>(listener, instances, triggers, listenerID, onFailure);
    }
}
public class OnInitializeEvent<TInstance> : OnInitializeEvent where TInstance : ILSEventable {
    public new TInstance Instance => (TInstance)base.Instance!;
    public static OnInitializeEvent<TInstance> Create(TInstance? instance, LSAction? onSuccess = null, LSMessageHandler? onFailure = null) {
        if (instance == null) throw new LSArgumentNullException(nameof(instance), "Instance cannot be null for OnInitializeEvent.");
        OnInitializeEvent<TInstance> @event = new OnInitializeEvent<TInstance>(instance);
        @event.SuccessCallback += onSuccess;
        @event.FailureCallback += onFailure;
        return @event;
    }
    protected OnInitializeEvent(TInstance instance) : base(instance) { }

}
