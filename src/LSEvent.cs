namespace LSUtils;

public abstract class LSEvent {
    #region Fields
    // protected static LSAction<float, LSAction>? _delayHandler;
    // internal static void setDelayHandler(LSAction<float, LSAction> handler) => _delayHandler = handler;
    protected readonly Semaphore _semaphore;
    //private readonly LSEventOptions _options;
    protected readonly LSDispatcher _dispatcher;
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
    public event LSAction<string> FailureCallback {
        add { _semaphore.FailureCallback += value; }
        remove { _semaphore.FailureCallback -= value; }
    }
    #endregion
    public LSEvent(LSEventOptions? options) {
        options ??= new LSEventOptions();
        ID = options.ID;
        SuccessCallback += options.OnSuccess;
        FailureCallback += options.OnFailure;
        CancelCallback += options.OnCancel;
        _dispatcher = options.Dispatcher == null ? LSDispatcher.Instance : options.Dispatcher;
        GroupType = options.GroupType;
        _semaphore = Semaphore.Create();
    }
    public virtual bool Dispatch() {
        ListenerGroupEntry searchGroup = ListenerGroupEntry.Create(LSEventType.Get(GetType()), GroupType, GetInstances());
        HasDispatched = true;
        return _dispatcher.Dispatch(searchGroup, this);
    }
    public void Wait() => _semaphore.Wait();
    public void Wait(float delayValue, LSAction? delayCallback = null, System.Guid signalID = default) {
        if (delayValue <= 0f) return;
        _semaphore.Wait(signalID);
        if (delayCallback != null) SuccessCallback += delayCallback;
        _dispatcher.Delay(delayValue, _semaphore.Signal);
    }
    public void Signal() => _semaphore.Signal();
    public int Signal(out System.Guid signalID) => _semaphore.Signal(out signalID);
    public void Failure(string msg) => _semaphore.Failure(msg);
    public int Failure(out System.Guid signalID, string msg) => _semaphore.Failure(out signalID, msg);
    public void Cancel() => _semaphore.Cancel();
    public int Cancel(out System.Guid[] remainingSignalIDs) => _semaphore.Cancel(out remainingSignalIDs);
    public virtual ILSEventable[] GetInstances() => new ILSEventable[0];
}
public abstract class LSEvent<TInstance> : LSEvent where TInstance : ILSEventable {
    protected ILSEventable[] _instances;
    /// <summary>
    /// Creates a new <see cref="LSEvent"/> with a primary instance.
    /// The event type is determined by the type parameter <typeparamref name="TInstance"/>.
    /// The event group type is set to <see cref="ListenerGroupType.SUBSET"/>.
    /// </summary>
    /// <param name="instance">The primary instance.</param>
    public TInstance? Instance => _instances.Length > 0 ? (TInstance)_instances[0] : default(TInstance);

    protected LSEvent(ILSEventable[] instances, LSEventOptions? options) : base(options) {
        _instances = instances;
    }
    protected LSEvent(ILSEventable instance, LSEventOptions? options) : base(options) {
        _instances = new ILSEventable[] { instance };
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
    //protected LSEvent(TPrimaryInstance primaryInstance, TSecondaryInstance secondaryInstance) : base(System.Guid.NewGuid(), (new ILSEventable[] { primaryInstance, secondaryInstance }).Where(x => x != null).ToArray(), groupType) { }
    public TSecondaryInstance? SecondaryInstance => (TSecondaryInstance)_instances[1] ?? throw new LSException($"{{secondary_instance_null}}");
    protected LSEvent(TPrimaryInstance primaryInstance, TSecondaryInstance secondaryInstance, LSEventOptions? options) : base(new ILSEventable[] { primaryInstance, secondaryInstance }, options) {
        if (primaryInstance == null) throw new LSArgumentNullException(nameof(primaryInstance), "{primary_instance_null}");
        if (secondaryInstance == null) throw new LSArgumentNullException(nameof(secondaryInstance), "{secondary_instance_null}");
    }
}
public abstract class OnInitializeEvent : LSEvent<ILSEventable> {
    public static OnInitializeEvent<TInstance> Create<TInstance>(TInstance instance, LSEventOptions? options) where TInstance : ILSEventable {
        return OnInitializeEvent<TInstance>.Create(instance, options);
    }
    public static System.Guid Register<TInstance>(LSListener<OnInitializeEvent<TInstance>> listener, ILSEventable[] instances = null!, int triggers = -1, System.Guid listenerID = default, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) where TInstance : ILSEventable {
        dispatcher ??= LSDispatcher.Instance;
        return dispatcher.Register<OnInitializeEvent<TInstance>>(listener, instances, triggers, listenerID, onFailure);
    }

    protected OnInitializeEvent(ILSEventable instance, LSEventOptions? options) : base(instance, options) { }
}
public class OnInitializeEvent<TInstance> : OnInitializeEvent where TInstance : ILSEventable {
    public new TInstance Instance => (TInstance)base.Instance!;
    public static OnInitializeEvent<TInstance> Create(TInstance instance, LSEventOptions? options) {
        OnInitializeEvent<TInstance> @event = new OnInitializeEvent<TInstance>(instance, options);
        return @event;
    }
    protected OnInitializeEvent(TInstance instance, LSEventOptions? options) : base(instance, options) { }

}
public class LSEventOptions {
    public LSEventOptions() {
        OnFailure = (error) => ErrorHandler($"no_error_handler:{error}");
    }
    public LSEventOptions(LSEventOptions options) : this() {
        ID = options.ID;
        Dispatcher = options.Dispatcher ?? LSDispatcher.Instance;
        GroupType = options.GroupType;
        ErrorHandler = options.ErrorHandler;
        OnSuccess = options.OnSuccess;
        OnFailure = options.OnFailure;
        OnCancel = options.OnCancel;
    }
    public System.Guid ID { get; set; } = System.Guid.NewGuid();
    public LSDispatcher Dispatcher { get; set; } = LSDispatcher.Instance;
    public ListenerGroupType GroupType { get; set; } = ListenerGroupType.STATIC;
    public LSMessageHandler ErrorHandler { get; set; } = (error) => throw new LSException($"no_error_handler:{error}");
    public LSAction? OnSuccess { get; set; } = null;
    public LSAction<string> OnFailure { get; set; }
    public LSAction? OnCancel { get; set; } = null;
}
