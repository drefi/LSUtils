namespace LSUtils;

public abstract class LSState<TState, TContext> : ILSState where TState : LSState<TState, TContext> where TContext : ILSContext {
    private LSAction<ILSState>? _exitCallback;
    public virtual System.Guid ID { get; }
    public TContext Context { get; protected set; }
    public virtual string ClassName => nameof(LSState<TState, TContext>);

    protected virtual void onInitialize(OnInitializeEvent<TState> @event) { }
    protected virtual void onEnter(OnEnterEvent @event) { }
    protected virtual void onExit(OnExitEvent @event) { }
    protected LSState(TContext context) {
        ID = System.Guid.NewGuid();
        Context = context;
    }

    public bool Initialize(LSEventOptions? options = null) {
        ILSEventable[] instances = new ILSEventable[] { Context, this };
        var @event = OnInitializeEvent.Create<TState>((TState)this, options);
        onInitialize(@event);
        return @event.Dispatch();
    }
    public void Enter<T>(LSAction<T> enterCallback, LSAction<T> exitCallback, LSEventOptions options) where T : ILSState {
        //TODO: Enter need to have an OnEnterEvent<TState> in the same way as OnInitializeEvent<TState>
        _exitCallback = exitCallback == null ? null : new LSAction<ILSState>((state) => { exitCallback((T)(object)state); });
        LSAction? successCallback = enterCallback == null ? null : new LSAction(() => enterCallback((T)(object)this));
        if (this is not TState stateInstance)
            throw new System.InvalidCastException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var @event = new OnEnterEvent(Context, stateInstance, options);
        onEnter(@event);
        @event.SuccessCallback += successCallback;
        @event.Dispatch();
    }
    public void Exit(LSEventOptions? options = null) {
        if (this is not TState stateInstance)
            throw new System.InvalidCastException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var @event = new OnExitEvent(Context, stateInstance, options);
        onExit(@event);
        if (_exitCallback != null) @event.SuccessCallback += () => _exitCallback(this);
        @event.Dispatch();
    }

    public virtual void Cleanup() {
        throw new NotImplementedException();
    }
    #region Events
    public class OnEnterEvent : LSEvent<TContext, TState> {
        public TContext Context { get; }
        public TState State { get; }
        public OnEnterEvent(TContext context, TState instance, LSEventOptions? eventOptions) : base(context, instance, eventOptions) {
            Context = context;
            State = instance;
        }
    }
    public class OnExitEvent : LSEvent<TContext, TState> {
        public TContext Context { get; }
        public TState State { get; }
        public OnExitEvent(TContext context, TState instance, LSEventOptions? options) : base(context, instance, options) {
            Context = context;
            State = instance;
        }
    }
    #endregion
}
