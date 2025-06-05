namespace LSUtils;

public abstract class LSState<TState, TContext> : ILSState where TState : LSState<TState, TContext> where TContext : ILSContext {
    private LSAction<ILSState>? _exitCallback;
    public virtual System.Guid ID { get; }
    public TContext Context { get; protected set; }
    public virtual string ClassName => nameof(LSState<TState, TContext>);

    protected virtual void onInitialize(System.Guid listenerID, OnInitializeEvent @event) { }
    protected virtual void onEnter(System.Guid listenerID, OnEnterEvent @event) { }
    protected virtual void onExit(System.Guid listenerID, OnExitEvent @event) { }
    protected LSState(TContext context) {
        ID = System.Guid.NewGuid();
        Context = context;
    }

    public bool Initialize(LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        ILSEventable[] instances = new ILSEventable[] { Context, this };
        LSEvent.Register<OnInitializeEvent<TState>>(onInitialize, new ILSEventable[] { this }, 1, default, onFailure, dispatcher);
        LSEvent.Register<OnEnterEvent>(onEnter, instances, -1, default, onFailure, dispatcher);
        LSEvent.Register<OnExitEvent>(onExit, instances, -1, default, onFailure, dispatcher);
        OnInitializeEvent @event = OnInitializeEvent.Create<TState>((TState)this, onSuccess, onFailure);
        return @event.Dispatch(onFailure, dispatcher);
    }
    public void Enter<T>(LSAction<T>? enterCallback = null, LSAction<T>? exitCallback = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) where T : ILSState {
        _exitCallback = exitCallback == null ? null : new LSAction<ILSState>((state) => { exitCallback((T)(object)state!); });
        LSAction? successCallback = enterCallback == null ? null : new LSAction(() => enterCallback((T)(object)this));
        if (this is not TState stateInstance)
            throw new System.InvalidCastException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        OnEnterEvent @event = new OnEnterEvent(Context, stateInstance);
        @event.SuccessCallback += successCallback;
        @event.FailureCallback += onFailure;
        @event.Dispatch(onFailure, dispatcher);
    }
    public void Exit(LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (this is not TState stateInstance)
            throw new System.InvalidCastException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        OnExitEvent @event = new OnExitEvent(Context, stateInstance);
        LSAction? successCallback = onSuccess != null || _exitCallback != null ? new LSAction(() => {
            _exitCallback?.Invoke(this);
            onSuccess?.Invoke();
        }) : null;
        @event.SuccessCallback += successCallback;
        @event.FailureCallback += onFailure;
        @event.Dispatch(onFailure, dispatcher);
    }
    #region Events
    public class OnEnterEvent : LSEvent<TContext, TState> {
        public TContext Context { get; }
        public TState State { get; }
        public OnEnterEvent(TContext context, TState instance) : base(context, instance) {
            Context = context;
            State = instance;
        }
    }
    public class OnExitEvent : LSEvent<TContext, TState> {
        public TContext Context { get; }
        public TState State { get; }
        public OnExitEvent(TContext context, TState instance) : base(context, instance) {
            Context = context;
            State = instance;
        }
    }
    #endregion
}
