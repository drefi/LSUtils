namespace LSUtils;

/// <summary>
/// Abstract base class for implementing state machines with event-driven lifecycle management.
/// Provides a strongly-typed state implementation with context support and automatic event handling
/// for state transitions including initialization, entry, and exit operations.
/// </summary>
/// <typeparam name="TState">
/// The concrete state type that inherits from this base class. This enables strongly-typed
/// access to state-specific functionality and ensures type safety in state transitions.
/// </typeparam>
/// <typeparam name="TContext">
/// The context type that provides shared data and services to the state. Must implement
/// <see cref="ILSContext"/> to ensure proper context contract compliance.
/// </typeparam>
/// <remarks>
/// This class implements the State pattern with event-driven lifecycle management. Each state
/// maintains its own context and provides virtual methods for handling initialization, entry,
/// and exit events. The state lifecycle follows the pattern: Initialize → Enter → [Active] → Exit.
/// States are designed to be immutable after construction and thread-safe for concurrent access.
/// </remarks>
/// <example>
/// <code>
/// public class GamePlayState : LSState&lt;GamePlayState, GameContext&gt; {
///     public GamePlayState(GameContext context) : base(context) { }
///     
///     protected override void onInitialize(OnInitializeEvent&lt;GamePlayState&gt; @event) {
///         // Setup initial state
///         Context.LoadLevel();
///         @event.Signal(); // Complete initialization
///     }
///     
///     protected override void onEnter(OnEnterEvent @event) {
///         // Start gameplay
///         Context.StartGameLoop();
///         @event.Signal(); // Complete entry
///     }
///     
///     protected override void onExit(OnExitEvent @event) {
///         // Cleanup gameplay
///         Context.StopGameLoop();
///         @event.Signal(); // Complete exit
///     }
/// }
/// </code>
/// </example>
public abstract class LSState<TState, TContext> : ILSState 
    where TState : LSState<TState, TContext> 
    where TContext : ILSContext {

    #region Private Fields
    /// <summary>
    /// Callback to execute when the state exits, if one was provided during entry.
    /// </summary>
    private LSAction<ILSState>? _exitCallback;
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets the unique identifier for this state instance.
    /// </summary>
    /// <value>A GUID that uniquely identifies this state instance.</value>
    /// <remarks>
    /// The ID is generated automatically during construction and remains constant
    /// throughout the state's lifetime. This can be used for state tracking,
    /// debugging, and event correlation.
    /// </remarks>
    public virtual System.Guid ID { get; }

    /// <summary>
    /// Gets the context associated with this state.
    /// </summary>
    /// <value>The context instance that provides shared data and services to this state.</value>
    /// <remarks>
    /// The context is set during construction and provides access to shared resources,
    /// services, and data that the state needs to perform its operations. The context
    /// should remain consistent throughout the state's lifecycle.
    /// </remarks>
    public TContext Context { get; protected set; }

    /// <summary>
    /// Gets the class name of this state instance.
    /// </summary>
    /// <value>Returns the generic type name including type parameters.</value>
    /// <remarks>
    /// This property can be overridden by derived classes to provide more specific
    /// naming for debugging and logging purposes.
    /// </remarks>
    public virtual string ClassName => nameof(LSState<TState, TContext>);
    #endregion

    #region Virtual Event Handlers
    /// <summary>
    /// Called when the state is being initialized. Override this method to perform state setup.
    /// </summary>
    /// <param name="event">
    /// The initialization event that provides context and synchronization for the initialization process.
    /// </param>
    /// <remarks>
    /// This method is called during the <see cref="Initialize"/> process and should contain logic
    /// for setting up the state's initial conditions. The event must be signaled (via <see cref="LSEvent.Signal()"/>)
    /// to indicate completion of initialization. Failure to signal will leave the state in an incomplete state.
    /// </remarks>
    protected virtual void onInitialize(OnInitializeEvent<TState> @event) { }

    /// <summary>
    /// Called when the state is being entered. Override this method to perform entry operations.
    /// </summary>
    /// <param name="event">
    /// The enter event that provides context and synchronization for the entry process.
    /// </param>
    /// <remarks>
    /// This method is called during state entry and should contain logic for activating the state.
    /// The event must be signaled to indicate completion of entry operations. Entry callbacks
    /// registered during the <see cref="Enter{T}"/> call will be executed after successful completion.
    /// </remarks>
    protected virtual void onEnter(OnEnterEvent @event) { }

    /// <summary>
    /// Called when the state is being exited. Override this method to perform cleanup operations.
    /// </summary>
    /// <param name="event">
    /// The exit event that provides context and synchronization for the exit process.
    /// </param>
    /// <remarks>
    /// This method is called during state exit and should contain logic for deactivating the state
    /// and cleaning up resources. The event must be signaled to indicate completion of exit operations.
    /// Exit callbacks registered during state entry will be executed after successful completion.
    /// </remarks>
    protected virtual void onExit(OnExitEvent @event) { }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the state with the specified context.
    /// </summary>
    /// <param name="context">
    /// The context instance that provides shared data and services to this state.
    /// </param>
    /// <remarks>
    /// The constructor generates a unique ID for the state and stores the provided context.
    /// The context should contain all necessary dependencies and shared data that the state
    /// will need during its lifecycle.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="context"/> is null.
    /// </exception>
    protected LSState(TContext context) {
        ID = System.Guid.NewGuid();
        Context = context;
    }
    #endregion

    #region State Lifecycle Management
    /// <summary>
    /// Initializes the state by creating and dispatching an initialization event.
    /// </summary>
    /// <param name="eventOptions">
    /// Configuration options for the initialization event, including dispatcher, callbacks, and timeout.
    /// </param>
    /// <remarks>
    /// This method creates an initialization event, calls the virtual <see cref="onInitialize"/> method,
    /// and dispatches the event to any registered listeners. The state should only be used after
    /// successful initialization. This method should typically be called once after state creation.
    /// </remarks>
    /// <example>
    /// <code>
    /// var state = new MyGameState(context);
    /// var options = LSEventIOptions.Create();
    /// state.Initialize(options);
    /// // State is now ready for use
    /// </code>
    /// </example>
    public void Initialize(LSEventIOptions eventOptions) {
        var @event = OnInitializeEvent.Create<TState>((TState)this, eventOptions);
        onInitialize(@event);
        @event.Dispatch();
    }

    /// <summary>
    /// Enters the state with specified callbacks for entry completion and exit preparation.
    /// </summary>
    /// <typeparam name="T">
    /// The expected state type for the callbacks. Must implement <see cref="ILSState"/>.
    /// </typeparam>
    /// <param name="enterCallback">
    /// Optional callback to execute when state entry completes successfully.
    /// Receives the state instance as a parameter.
    /// </param>
    /// <param name="exitCallback">
    /// Optional callback to execute when the state exits in the future.
    /// This callback is stored and executed during the exit process.
    /// </param>
    /// <param name="options">Configuration options for the entry event.</param>
    /// <remarks>
    /// This method manages the state entry process by creating an enter event, storing the exit callback
    /// for future use, calling the virtual <see cref="onEnter"/> method, and dispatching the event.
    /// The enter callback is executed immediately upon successful entry, while the exit callback
    /// is stored and executed when <see cref="Exit"/> is called.
    /// </remarks>
    /// <exception cref="LSException">
    /// Thrown when the state cannot be cast to the expected type <typeparamref name="TState"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// state.Enter&lt;MyGameState&gt;(
    ///     enterCallback: (s) => Console.WriteLine($"Entered state {s.ID}"),
    ///     exitCallback: (s) => Console.WriteLine($"Exiting state {s.ID}"),
    ///     options: new LSEventIOptions()
    /// );
    /// </code>
    /// </example>
    public void Enter<T>(LSAction<T> enterCallback, LSAction<T> exitCallback, LSEventIOptions options) where T : ILSState {
        //TODO: Enter need to have an OnEnterEvent<TState> in the same way as OnInitializeEvent<TState>
        _exitCallback = exitCallback == null ? null : new LSAction<ILSState>((state) => { exitCallback((T)(object)state); });
        LSAction? successCallback = enterCallback == null ? null : new LSAction(() => enterCallback((T)(object)this));
        if (this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var @event = OnEnterEvent.Create(stateInstance, options);
        onEnter(@event);
        @event.SuccessCallback += successCallback;
        @event.Dispatch();
    }

    /// <summary>
    /// Exits the state by dispatching an exit event and executing any stored exit callback.
    /// </summary>
    /// <param name="eventOptions">Configuration options for the exit event.</param>
    /// <remarks>
    /// This method manages the state exit process by creating an exit event, calling the virtual
    /// <see cref="onExit"/> method, setting up the stored exit callback to execute on success,
    /// and dispatching the event. The exit callback that was provided during <see cref="Enter{T}"/>
    /// will be executed after successful exit completion.
    /// </remarks>
    /// <exception cref="LSException">
    /// Thrown when the state cannot be cast to the expected type <typeparamref name="TState"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// // Exit the state with custom options
    /// state.Exit(new LSEventIOptions { 
    ///     Timeout = 5.0f // Allow 5 seconds for exit completion
    /// });
    /// </code>
    /// </example>
    public void Exit(LSEventIOptions eventOptions) {
        if (this is not TState stateInstance)
            throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TState).FullName}.");
        var @event = OnExitEvent.Create(stateInstance, eventOptions);
        onExit(@event);
        if (_exitCallback != null) @event.SuccessCallback += () => _exitCallback(this);
        @event.Dispatch();
    }

    /// <summary>
    /// Performs cleanup operations for the state. This method must be implemented by derived classes.
    /// </summary>
    /// <remarks>
    /// This method should release any resources, unregister listeners, and perform final cleanup
    /// operations before the state is disposed. The base implementation throws a 
    /// <see cref="LSNotImplementedException"/> to ensure that derived classes provide their own implementation.
    /// </remarks>
    /// <exception cref="LSNotImplementedException">
    /// Always thrown by the base implementation to indicate that derived classes must override this method.
    /// </exception>
    /// <example>
    /// <code>
    /// public override void Cleanup() {
    ///     // Unregister event listeners
    ///     LSDispatcher.Instance.Unregister(myListenerId);
    ///     
    ///     // Release resources
    ///     Context.DisposeResources();
    ///     
    ///     // Call base cleanup if needed
    ///     // base.Cleanup(); // Not needed since base throws exception
    /// }
    /// </code>
    /// </example>
    public virtual void Cleanup() {
        throw new LSNotImplementedException();
    }
    #endregion
    #region State Events
    /// <summary>
    /// Abstract base class for state entry events that are triggered when a state is being entered.
    /// </summary>
    /// <remarks>
    /// Entry events are part of the state lifecycle and provide a way for external listeners
    /// to be notified when states are entered. This enables reactive programming patterns
    /// and allows for cross-cutting concerns like logging, analytics, and state validation.
    /// </remarks>
    public abstract class OnEnterEvent : LSEvent<TState> {
        #region Static Factory Methods
        /// <summary>
        /// Creates a new entry event for the specified state instance.
        /// </summary>
        /// <typeparam name="TInstance">
        /// The specific state type being entered. Must inherit from <typeparamref name="TState"/>.
        /// </typeparam>
        /// <param name="instance">The state instance that is being entered.</param>
        /// <param name="eventOptions">
        /// Optional configuration for the entry event. If null, default options are used.
        /// </param>
        /// <returns>A new strongly-typed entry event for the specified state instance.</returns>
        /// <remarks>
        /// This factory method provides type safety and convenience for creating entry events.
        /// The returned event can be used to register listeners or dispatch notifications.
        /// </remarks>
        public static OnEnterEvent<TInstance> Create<TInstance>(TInstance instance, LSEventIOptions? eventOptions) where TInstance : TState {
            eventOptions ??= LSEventIOptions.Create();
            return OnEnterEvent<TInstance>.Create(instance, eventOptions);
        }

        /// <summary>
        /// Registers a listener for entry events of the specified state type.
        /// </summary>
        /// <typeparam name="TInstance">
        /// The specific state type to listen for entry events from.
        /// </typeparam>
        /// <param name="listener">The listener callback to register.</param>
        /// <param name="instances">
        /// Optional array of specific state instances to listen for. If null, listens to all instances.
        /// </param>
        /// <param name="triggers">
        /// The number of times the listener should be triggered. Use -1 for unlimited triggers.
        /// </param>
        /// <param name="listenerID">
        /// Optional unique identifier for the listener. If not provided, a new GUID is generated.
        /// </param>
        /// <param name="dispatcher">
        /// Optional dispatcher to register with. If null, uses the default instance.
        /// </param>
        /// <returns>The unique identifier of the registered listener.</returns>
        /// <remarks>
        /// This method provides a convenient way to register listeners for state entry events
        /// without needing to manually interact with the dispatcher.
        /// </remarks>
        public static System.Guid Register<TInstance>(LSListener<OnEnterEvent<TInstance>> listener, ILSEventable[] instances = null!, int triggers = -1, System.Guid listenerID = default, LSDispatcher? dispatcher = null) where TInstance : TState {
            return OnEnterEvent<TInstance>.Register(listener, instances, triggers, listenerID, dispatcher);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the state instance that is being entered.
        /// </summary>
        /// <value>The state instance associated with this entry event.</value>
        /// <remarks>
        /// This property provides access to the state that triggered the entry event,
        /// allowing listeners to inspect state properties and context.
        /// </remarks>
        public TState State { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the entry event for the specified state.
        /// </summary>
        /// <param name="instance">The state instance being entered.</param>
        /// <param name="eventOptions">Configuration options for the event.</param>
        /// <remarks>
        /// This constructor is used internally by the state system to create entry events
        /// when states are being entered.
        /// </remarks>
        internal OnEnterEvent(TState instance, LSEventIOptions eventOptions) : base(instance, eventOptions) {
            State = instance;
        }
        #endregion
    }

    /// <summary>
    /// Strongly-typed entry event for a specific state instance type.
    /// </summary>
    /// <typeparam name="TInstance">
    /// The specific state type being entered. Must inherit from <typeparamref name="TState"/>.
    /// </typeparam>
    /// <remarks>
    /// This class provides a concrete implementation of entry events that is strongly-typed
    /// to a specific state type, providing type safety and better IntelliSense support.
    /// </remarks>
    public class OnEnterEvent<TInstance> : OnEnterEvent where TInstance : TState {
        #region Static Factory Methods
        /// <summary>
        /// Creates a new strongly-typed entry event for the specified state instance.
        /// </summary>
        /// <param name="instance">The state instance that is being entered.</param>
        /// <param name="eventOptions">
        /// Optional configuration for the entry event. If null, default options are used.
        /// </param>
        /// <returns>A new strongly-typed entry event for the specified state instance.</returns>
        /// <remarks>
        /// This factory method creates a concrete entry event that is strongly-typed
        /// to the specified state type, providing type safety and better IntelliSense support.
        /// </remarks>
        public static OnEnterEvent<TInstance> Create(TState instance, LSEventIOptions? eventOptions) {
            eventOptions ??= LSEventIOptions.Create();
            return new OnEnterEvent<TInstance>(instance, eventOptions);
        }

        /// <summary>
        /// Registers a strongly-typed listener for entry events of this specific state type.
        /// </summary>
        /// <param name="listener">The strongly-typed listener callback to register.</param>
        /// <param name="instances">
        /// Optional array of specific state instances to listen for. If null, listens to all instances.
        /// </param>
        /// <param name="triggers">
        /// The number of times the listener should be triggered. Use -1 for unlimited triggers.
        /// </param>
        /// <param name="listenerID">
        /// Optional unique identifier for the listener. If not provided, a new GUID is generated.
        /// </param>
        /// <param name="dispatcher">
        /// Optional dispatcher to register with. If null, uses the default instance.
        /// </param>
        /// <returns>The unique identifier of the registered listener.</returns>
        /// <remarks>
        /// This method registers a listener that will only receive entry events for the
        /// specific state type <typeparamref name="TInstance"/>, providing type safety.
        /// </remarks>
        public static System.Guid Register(LSListener<OnEnterEvent<TInstance>> listener, ILSEventable[] instances = null!, int triggers = -1, System.Guid listenerID = default, LSDispatcher? dispatcher = null) {
            dispatcher ??= LSDispatcher.Instance;
            return dispatcher.Register<OnEnterEvent<TInstance>>(listener, instances, triggers, listenerID);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the strongly-typed state instance being entered.
        /// </summary>
        /// <value>The state instance cast to the specific type <typeparamref name="TInstance"/>.</value>
        /// <remarks>
        /// This property provides strongly-typed access to the state instance, hiding the base class's
        /// generic state property and providing better type safety and IntelliSense support.
        /// </remarks>
        public new TState State => (TState)base.State!;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the strongly-typed entry event.
        /// </summary>
        /// <param name="instance">The strongly-typed state instance being entered.</param>
        /// <param name="eventOptions">Configuration options for the event.</param>
        /// <remarks>
        /// This constructor creates a strongly-typed entry event for the specified state instance.
        /// </remarks>
        protected OnEnterEvent(TState instance, LSEventIOptions eventOptions) : base(instance, eventOptions) { }
        #endregion
    }

    /// <summary>
    /// Abstract base class for state exit events that are triggered when a state is being exited.
    /// </summary>
    /// <remarks>
    /// Exit events are part of the state lifecycle and provide a way for external listeners
    /// to be notified when states are exited. This enables cleanup operations, state transition
    /// logging, and coordination between different parts of the system during state changes.
    /// </remarks>
    public abstract class OnExitEvent : LSEvent<TState> {
        #region Static Factory Methods
        /// <summary>
        /// Creates a new exit event for the specified state instance.
        /// </summary>
        /// <typeparam name="TInstance">
        /// The specific state type being exited. Must inherit from <typeparamref name="TState"/>.
        /// </typeparam>
        /// <param name="instance">The state instance that is being exited.</param>
        /// <param name="eventOptions">
        /// Optional configuration for the exit event. If null, default options are used.
        /// </param>
        /// <returns>A new strongly-typed exit event for the specified state instance.</returns>
        /// <remarks>
        /// This factory method provides type safety and convenience for creating exit events.
        /// The returned event can be used to register listeners or dispatch notifications.
        /// </remarks>
        public static OnExitEvent<TInstance> Create<TInstance>(TInstance instance, LSEventIOptions? eventOptions) where TInstance : TState {
            return OnExitEvent<TInstance>.Create(instance, eventOptions);
        }

        /// <summary>
        /// Registers a listener for exit events of the specified state type.
        /// </summary>
        /// <typeparam name="TInstance">
        /// The specific state type to listen for exit events from.
        /// </typeparam>
        /// <param name="listener">The listener callback to register.</param>
        /// <param name="instances">
        /// Optional array of specific state instances to listen for. If null, listens to all instances.
        /// </param>
        /// <param name="triggers">
        /// The number of times the listener should be triggered. Use -1 for unlimited triggers.
        /// </param>
        /// <param name="listenerID">
        /// Optional unique identifier for the listener. If not provided, a new GUID is generated.
        /// </param>
        /// <param name="dispatcher">
        /// Optional dispatcher to register with. If null, uses the default instance.
        /// </param>
        /// <returns>The unique identifier of the registered listener.</returns>
        /// <remarks>
        /// This method provides a convenient way to register listeners for state exit events
        /// without needing to manually interact with the dispatcher.
        /// </remarks>
        public static System.Guid Register<TInstance>(LSListener<OnExitEvent<TInstance>> listener, ILSEventable[] instances = null!, int triggers = -1, System.Guid listenerID = default, LSDispatcher? dispatcher = null) where TInstance : TState {
            return OnExitEvent<TInstance>.Register(listener, instances, triggers, listenerID, dispatcher);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the state instance that is being exited.
        /// </summary>
        /// <value>The state instance associated with this exit event.</value>
        /// <remarks>
        /// This property provides access to the state that triggered the exit event,
        /// allowing listeners to perform cleanup operations or log state information.
        /// </remarks>
        public TState State { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the exit event for the specified state.
        /// </summary>
        /// <param name="instance">The state instance being exited.</param>
        /// <param name="eventOptions">Configuration options for the event.</param>
        /// <remarks>
        /// This constructor is used internally by the state system to create exit events
        /// when states are being exited.
        /// </remarks>
        internal OnExitEvent(TState instance, LSEventIOptions eventOptions) : base(instance, eventOptions) {
            State = instance;
        }
        #endregion
    }

    /// <summary>
    /// Strongly-typed exit event for a specific state instance type.
    /// </summary>
    /// <typeparam name="TInstance">
    /// The specific state type being exited. Must inherit from <typeparamref name="TState"/>.
    /// </typeparam>
    /// <remarks>
    /// This class provides a concrete implementation of exit events that is strongly-typed
    /// to a specific state type, providing type safety and better IntelliSense support.
    /// </remarks>
    public class OnExitEvent<TInstance> : OnExitEvent where TInstance : TState {
        #region Static Factory Methods
        /// <summary>
        /// Creates a new strongly-typed exit event for the specified state instance.
        /// </summary>
        /// <param name="instance">The state instance that is being exited.</param>
        /// <param name="eventOptions">
        /// Optional configuration for the exit event. If null, default options are used.
        /// </param>
        /// <returns>A new strongly-typed exit event for the specified state instance.</returns>
        /// <remarks>
        /// This factory method creates a concrete exit event that is strongly-typed
        /// to the specified state type, providing type safety and better IntelliSense support.
        /// </remarks>
        public static OnExitEvent<TInstance> Create(TState instance, LSEventIOptions? eventOptions) {
            eventOptions ??= LSEventIOptions.Create();
            return new OnExitEvent<TInstance>(instance, eventOptions);
        }

        /// <summary>
        /// Registers a strongly-typed listener for exit events of this specific state type.
        /// </summary>
        /// <param name="listener">The strongly-typed listener callback to register.</param>
        /// <param name="instances">
        /// Optional array of specific state instances to listen for. If null, listens to all instances.
        /// </param>
        /// <param name="triggers">
        /// The number of times the listener should be triggered. Use -1 for unlimited triggers.
        /// </param>
        /// <param name="listenerID">
        /// Optional unique identifier for the listener. If not provided, a new GUID is generated.
        /// </param>
        /// <param name="dispatcher">
        /// Optional dispatcher to register with. If null, uses the default instance.
        /// </param>
        /// <returns>The unique identifier of the registered listener.</returns>
        /// <remarks>
        /// This method registers a listener that will only receive exit events for the
        /// specific state type <typeparamref name="TInstance"/>, providing type safety.
        /// </remarks>
        public static System.Guid Register(LSListener<OnExitEvent<TInstance>> listener, ILSEventable[] instances = null!, int triggers = -1, System.Guid listenerID = default, LSDispatcher? dispatcher = null) {
            dispatcher ??= LSDispatcher.Instance;
            return dispatcher.Register<OnExitEvent<TInstance>>(listener, instances, triggers, listenerID);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the strongly-typed state instance being exited.
        /// </summary>
        /// <value>The state instance cast to the specific type <typeparamref name="TInstance"/>.</value>
        /// <remarks>
        /// This property provides strongly-typed access to the state instance, hiding the base class's
        /// generic state property and providing better type safety and IntelliSense support.
        /// </remarks>
        public new TState State => (TState)base.State!;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the strongly-typed exit event.
        /// </summary>
        /// <param name="instance">The strongly-typed state instance being exited.</param>
        /// <param name="eventOptions">Configuration options for the event.</param>
        /// <remarks>
        /// This constructor creates a strongly-typed exit event for the specified state instance.
        /// </remarks>
        protected OnExitEvent(TState instance, LSEventIOptions eventOptions) : base(instance, eventOptions) { }
        #endregion
    }
    #endregion
}
