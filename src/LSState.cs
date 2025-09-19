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
public abstract class LSState<TState, TContext> : ILSState, ILSEventable
    where TState : LSState<TState, TContext>
    where TContext : ILSContext {

    #region Public Properties
    public LSDispatcher? Dispatcher { get; protected set; }
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
    protected LSState(TContext context) {
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        ID = System.Guid.NewGuid();
    }
    #endregion

    public EventProcessResult Initialize(LSEventOptions options) {
        try {
            if (this is not TState stateInstance)
                throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
            Dispatcher = options.Dispatcher;
            var @event = new OnInitializeEvent(stateInstance, options);
            return @event.CancelPhaseIf<LSEventBusinessState.ValidatePhaseState>((evt, entry) => IsInitialized)
                .OnSucceed(evt => IsInitialized = true).Dispatch();
        } catch (LSException e) {
            throw new LSException($"Failed to initialize state {ClassName}.", e);
        }
    }

    #region ILSState Implementation
    protected LSAction? _exitCallback;
    public void Enter<T>(LSAction<T> enterCallback, LSAction<T> exitCallback) where T : ILSState {
        if (this is not T tState || this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().AssemblyQualifiedName} to {typeof(TState).AssemblyQualifiedName}.");
        if (Dispatcher == null)
            throw new LSException($"State {ClassName} is not initialized. Dispatcher is null.");
        _exitCallback = exitCallback == null ? null : () => exitCallback(tState);
        var options = new LSEventOptions(Dispatcher, this);
        var @event = new OnEnterEvent(options, stateInstance);
        @event.WithStateCallbacks<LSEventCompletedState>(register => register
            .When((evt, entry) => enterCallback != null)
            .Handler((evt) => {
                enterCallback?.Invoke(tState);
            })).Dispatch();
    }

    /// <summary>
    /// Exits the state by dispatching an exit event and executing any stored exit callback.
    /// </summary>
    public void Exit(LSAction callback) {
        if (this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var options = new LSEventOptions(Dispatcher, this);
        var @event = new OnExitEvent(options, stateInstance);
        @event.WithStateCallbacks<LSEventCompletedState>(register => register
            .When((evt, entry) => _exitCallback != null)
            .Handler((evt) => {
                _exitCallback?.Invoke();
                _exitCallback = null;
            }))
            .OnCompleted(evt => callback?.Invoke());
        @event.Dispatch();
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
    public class OnInitializeEvent : LSEvent_obsolete {
        public TState State { get; protected set; }
        public OnInitializeEvent(TState state, LSEventOptions options) : base(options) {
            State = state;
        }
    }

    /// <summary>
    /// Event triggered when a state is being entered.
    /// </summary>
    public class OnEnterEvent : LSEvent_obsolete {
        public TState State { get; protected set; }
        public OnEnterEvent(LSEventOptions options, TState state) : base(options) {
            State = state;
        }
    }

    /// <summary>
    /// Event triggered when a state is being exited.
    /// </summary>
    public class OnExitEvent : LSEvent_obsolete {
        public TState State { get; protected set; }
        public OnExitEvent(LSEventOptions options, TState state) : base(options) {
            State = state;
        }
    }
    #endregion
}
