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

    #region Private Fields
    private readonly LSDispatcher _dispatcher = new LSDispatcher();
    private LSAction? _exitCallback;
    #endregion

    #region Public Properties
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
    public virtual string ClassName => nameof(LSState<TState, TContext>);
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the state with the specified context.
    /// </summary>
    /// <param name="context">The context instance that provides shared data and services to this state.</param>
    protected LSState(TContext context) {
        ID = System.Guid.NewGuid();
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
    }
    #endregion

    #region Virtual Event Handlers
    /// <summary>
    /// Called when the state is being initialized. Override this method to perform state setup.
    /// </summary>
    /// <param name="event">The initialization event.</param>
    protected virtual void OnInitialize(StateInitializeEvent @event) { }

    /// <summary>
    /// Called when the state is being entered. Override this method to perform entry operations.
    /// </summary>
    /// <param name="event">The enter event.</param>
    protected virtual void OnEnter(StateEnterEvent @event) { }

    /// <summary>
    /// Called when the state is being exited. Override this method to perform cleanup operations.
    /// </summary>
    /// <param name="event">The exit event.</param>
    protected virtual void OnExit(StateExitEvent @event) { }
    #endregion

    #region ILSEventable Implementation
    /// <summary>
    /// Initializes the state with the new V2 event system.
    /// </summary>
    public void Initialize() {
        var @event = StateInitializeEvent.Create((TState)this);
        OnInitialize(@event);
        _dispatcher.ProcessEvent(@event);
    }
    #endregion

    #region ILSState Implementation
    /// <summary>
    /// Enters the state with specified callbacks.
    /// </summary>
    /// <typeparam name="T">The expected state type for the callbacks.</typeparam>
    /// <param name="enterCallback">Optional callback to execute when state entry completes successfully.</param>
    /// <param name="exitCallback">Optional callback to execute when the state exits in the future.</param>
    public void Enter<T>(LSAction<T> enterCallback, LSAction<T> exitCallback) where T : ILSState {
        // Store exit callback for future use
        _exitCallback = exitCallback == null ? null : new LSAction(() => exitCallback((T)(object)this));

        // Create success callback for immediate execution
        LSAction? successCallback = enterCallback == null ? null : new LSAction(() => enterCallback((T)(object)this));

        if (this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");

        var @event = StateEnterEvent.Create(stateInstance);
        OnEnter(@event);

        // Execute success callback if provided
        successCallback?.Invoke();

        _dispatcher.ProcessEvent(@event);
    }

    /// <summary>
    /// Exits the state by dispatching an exit event and executing any stored exit callback.
    /// </summary>
    public void Exit() {
        if (this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");

        var @event = StateExitEvent.Create(stateInstance);
        OnExit(@event);

        // Execute stored exit callback if any
        _exitCallback?.Invoke();

        _dispatcher.ProcessEvent(@event);
    }
    #endregion

    #region Abstract Methods
    /// <summary>
    /// Performs cleanup operations for the state. Must be implemented by derived classes.
    /// </summary>
    public abstract void Cleanup();
    #endregion

    #region V2 Event Classes
    /// <summary>
    /// Event triggered when a state is being initialized.
    /// </summary>
    public class StateInitializeEvent : LSEvent<TState> {
        public TState State => Instance;

        protected StateInitializeEvent(TState state) : base(state) { }

        public static StateInitializeEvent Create(TState state) => new StateInitializeEvent(state);
    }

    /// <summary>
    /// Event triggered when a state is being entered.
    /// </summary>
    public class StateEnterEvent : LSEvent<TState> {
        public TState State => Instance;

        protected StateEnterEvent(TState state) : base(state) { }

        public static StateEnterEvent Create(TState state) => new StateEnterEvent(state);
    }

    /// <summary>
    /// Event triggered when a state is being exited.
    /// </summary>
    public class StateExitEvent : LSEvent<TState> {
        public TState State => Instance;

        protected StateExitEvent(TState state) : base(state) { }

        public static StateExitEvent Create(TState state) => new StateExitEvent(state);
    }
    #endregion
}
