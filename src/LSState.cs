using LSUtils.EventSystem;

namespace LSUtils;

/// <summary>
/// Modern state implementation using the V2 CleanEventDispatcher system.
/// Provides a clean, phase-based approach to state management with strong typing.
/// </summary>
/// <typeparam name="TState">The concrete state type that inherits from this base class.</typeparam>
/// <typeparam name="TContext">The context type that provides shared data and services to the state.</typeparam>
/// <remarks>
/// This is a modernized version of the state system that uses the V2 CleanEventDispatcher
/// for event handling. It maintains compatibility with the ILSState interface while providing
/// improved performance and cleaner architecture.
/// </remarks>
public abstract class LSState<TState, TContext> : ILSState
    where TState : LSState<TState, TContext>
    where TContext : ILSContext {

    #region Public Properties
    public LSLegacyDispatcher Dispatcher { get; }
    public bool IsInitialized { get; protected set; }
    /// <summary>
    /// Gets the unique identifier for this state instance.
    /// </summary>
    public virtual System.Guid ID { get; }

    /// <summary>
    /// Gets the context associated with this state.
    /// </summary>
    public TContext Context { get; protected set; }

    /// <summary>
    /// Gets the class name of this state instance.
    /// </summary>
    public virtual string ClassName => $"{nameof(LSState<TState, TContext>)}<{typeof(TState).Name}, {typeof(TContext).Name}>";
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the state with the specified context.
    /// </summary>
    /// <param name="context">The context instance that provides shared data and services to this state.</param>
    protected LSState(TContext context, LSLegacyDispatcher? dispatcher = null) {
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        Dispatcher = dispatcher ?? LSLegacyDispatcher.Singleton;
        ID = System.Guid.NewGuid();
    }
    #endregion

    public void Initialize() {
        if (this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var @event = new OnInitializeEvent(stateInstance);
        @event.WithCallbacks<OnInitializeEvent>(Dispatcher)
            .CancelIf(evt => IsInitialized)
            .Dispatch();
        IsInitialized = true;
    }

    #region ILSState Implementation
    protected LSAction? _exitCallback;
    public void Enter<T>(LSAction<T> enterCallback, LSAction<T> exitCallback) where T : ILSState {
        if (this is not T tState || this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        _exitCallback = exitCallback == null ? null : () => exitCallback(tState);
        var @event = new OnEnterEvent(stateInstance);
        @event.WithCallbacks<OnEnterEvent>(Dispatcher)
            .Dispatch();
        enterCallback?.Invoke(tState);
    }

    /// <summary>
    /// Exits the state by dispatching an exit event and executing any stored exit callback.
    /// </summary>
    public void Exit(LSAction callback) {
        if (this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var @event = new OnExitEvent(stateInstance);
        @event.WithCallbacks<OnExitEvent>(Dispatcher)
            .Dispatch();
        _exitCallback?.Invoke();
        _exitCallback = null;
        callback?.Invoke();
    }
    #endregion

    #region Abstract Methods
    /// <summary>
    /// Performs cleanup operations for the state. Must be implemented by derived classes.
    /// </summary>
    public abstract void Cleanup();
    #endregion

    #region Event Classes
    /// <summary>
    /// Event triggered when a state is being initialized.
    /// </summary>
    public class OnInitializeEvent : LSLegacyEvent<TState> {
        public TState State => Instance;
        public OnInitializeEvent(TState state) : base(state) { }
    }

    /// <summary>
    /// Event triggered when a state is being entered.
    /// </summary>
    public class OnEnterEvent : LSLegacyEvent<TState> {
        public TState State => Instance;
        public OnEnterEvent(TState state) : base(state) { }
    }

    /// <summary>
    /// Event triggered when a state is being exited.
    /// </summary>
    public class OnExitEvent : LSLegacyEvent<TState> {
        public TState State => Instance;
        public OnExitEvent(TState state) : base(state) { }
    }
    #endregion
}
