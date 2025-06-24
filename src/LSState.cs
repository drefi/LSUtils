namespace LSUtils;

public abstract class LSState<TState, TContext> : ILSState where TState : LSState<TState, TContext> where TContext : ILSContext {
    private LSAction<ILSState>? _exitCallback;
    public virtual System.Guid ID { get; }
    public TContext Context { get; protected set; }
    public virtual string ClassName => nameof(LSState<TState, TContext>);

    protected virtual void onInitialize(System.Guid listenerID, OnInitializeEvent<TState> @event) { }
    protected virtual void onEnter(System.Guid listenerID, OnEnterEvent @event) { }
    protected virtual void onExit(System.Guid listenerID, OnExitEvent @event) { }
    protected LSState(TContext context) {
        ID = System.Guid.NewGuid();
        Context = context;
    }

    public bool Initialize(LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        ILSEventable[] instances = new ILSEventable[] { Context, this };
        var @event = OnInitializeEvent.Create<TState>((TState)this, onSuccess, onFailure);
        onInitialize(default, @event);
        return @event.Dispatch(onFailure, dispatcher);
    }
    public void Enter<T>(LSAction<T>? enterCallback = null, LSAction<T>? exitCallback = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) where T : ILSState {
        _exitCallback = exitCallback == null ? null : new LSAction<ILSState>((state) => { exitCallback((T)(object)state!); });
        LSAction? successCallback = enterCallback == null ? null : new LSAction(() => enterCallback((T)(object)this));
        if (this is not TState stateInstance)
            throw new System.InvalidCastException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var @event = new OnEnterEvent(Context, stateInstance);
        onEnter(default, @event);
        @event.SuccessCallback += successCallback;
        @event.FailureCallback += onFailure;
        @event.Dispatch(onFailure, dispatcher);
    }
    public void Exit(LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (this is not TState stateInstance)
            throw new System.InvalidCastException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var @event = new OnExitEvent(Context, stateInstance);
        onExit(default, @event);
        if (_exitCallback != null) @event.SuccessCallback += () => _exitCallback(this);
        if (onSuccess != null) @event.SuccessCallback += onSuccess;
        @event.FailureCallback += onFailure;
        @event.Dispatch(onFailure, dispatcher);
    }

    public void Cleanup() {
        throw new NotImplementedException();
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
