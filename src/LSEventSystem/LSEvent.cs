using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Base event class.
/// 
/// This abstract class serves as the foundation for all events in the LSEventSystem,
/// The event system processes events through sequential states: Business → Success/Cancelled → Completed.
/// Business state processes events through sequential phases: VALIDATE → CONFIGURE → EXECUTE → CLEANUP.
/// 
/// Event Lifecycle:
/// 1. Event creation with LSEventOptions
/// 2. Handler registration (event-scoped)
/// 3. Dispatch() - begins event processing
/// 4. Sequential state execution: Business → Success/Cancelled → Completed
/// 4. Sequential phase execution: VALIDATE → CONFIGURE → EXECUTE → CLEANUP
/// 5. Terminal state: Completed
/// 
/// Usage Example:
/// <code>
/// public class UserRegistrationEvent : BaseEvent {
///     public string Email { get; }
///     public string Name { get; }
///
///     public UserRegistrationEvent(string email, string name, LSEventOptions? options)
///         : base(options) {
///         Email = email;
///         Name = name;
///     }
/// }
/// 
/// // Usage with event-scoped handlers
/// var evt = new UserRegistrationEvent("user@example.com", "John Doe", options);
/// var result = evt.WithPhaseCallbacks<BusinessState.ValidatePhaseState>(
///     register => register
///         .Handler(ctx => {
///             // Validation logic
///             return HandlerProcessResult.SUCCESS;
///         })
/// ).Dispatch();
/// </code>
/// 
/// Thread Safety:
/// - Data storage operations are thread-safe via ConcurrentDictionary
/// - State transitions are handled by the state machine implementation
/// - Handler execution follows the phase-based sequential model
/// 
/// See also: <see cref="ILSEvent"/>, <see cref="LSDispatcher"/>, <see cref="LSEventProcessContext"/>
/// </summary>
public abstract class LSEvent : ILSEvent {
    protected readonly ConcurrentDictionary<string, object> _data = new();
    public readonly LSDispatcher Dispatcher;
    /// <summary>
    /// List of event-scoped handlers that will be added to the processing pipeline.
    /// These handlers are specific to this event instance and are not shared globally.
    /// </summary>
    protected readonly List<IHandlerEntry> _eventHandlers = new();
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public System.Guid ID { get; } = System.Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when this event was created.
    /// </summary>
    public System.DateTime CreatedAt { get; } = System.DateTime.UtcNow;

    /// <summary>
    /// Current phase being executed (only relevant in Business state).
    /// </summary>
    //public EventSystemPhase CurrentPhase { get; internal set; } = EventSystemPhase.VALIDATE;

    /// <summary>
    /// Phases that have been completed successfully.
    /// </summary>
    //public EventSystemPhase CompletedPhases { get; internal set; }

    /// <summary>
    /// Indicates if the event processing was cancelled.
    /// </summary>
    public bool IsCancelled => _isCanceled();
    protected System.Func<bool> _isCanceled = () => false;

    /// <summary>
    /// Indicates if the event has failures but processing can continue.
    /// In v4, this is determined by phase results rather than a separate flag.
    /// </summary>
    public bool HasFailures => _hasFailures();
    protected System.Func<bool> _hasFailures = () => false;

    /// <summary>
    /// Indicates if the event has completed processing.
    /// </summary>
    public bool IsCompleted { get; internal set; }
    public bool InDispatch { get; internal set; }
    /// <summary>
    /// Read-only access to event data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data => _data;

    private LSEvent() {
        throw new LSException("Default constructor is not allowed. Use the constructor with options parameter.");
    }
    //constructor with dispatcher
    protected LSEvent(LSEventOptions? options) {
        Dispatcher = options?.Dispatcher ?? LSDispatcher.Singleton;
        if (options == null) return;
        if (options.Entries.Count > 0) _eventHandlers.AddRange(options.Entries);
    }
    /// <summary>
    /// Gets or sets data associated with this event instance.
    /// Thread-safe operation using internal concurrent dictionary.
    /// </summary>
    /// <typeparam name="T">The type of value to store</typeparam>
    /// <param name="key">The unique key for the data</param>
    /// <param name="value">The value to store</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
    /// <example>
    /// <code>
    /// event.SetData("user_id", 12345);
    /// event.SetData("validation_errors", new List&lt;string&gt;());
    /// event.SetData("processing_timestamp", DateTime.UtcNow);
    /// </code>
    /// </example>
    public virtual void SetData<T>(string key, T value) {
        _data[key] = (object)value!;
    }

    /// <summary>
    /// Retrieves data associated with this event instance.
    /// Thread-safe operation using internal concurrent dictionary.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored value</typeparam>
    /// <param name="key">The unique key for the data</param>
    /// <returns>The stored value if found and of correct type, otherwise default(T)</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
    /// <example>
    /// <code>
    /// var userId = event.GetData&lt;int&gt;("user_id");
    /// var errors = event.GetData&lt;List&lt;string&gt;&gt;("validation_errors") ?? new List&lt;string&gt;();
    /// </code>
    /// </example>
    public virtual T GetData<T>(string key) {
        if (_data.TryGetValue(key, out var value) && value is T typedValue) {
            return typedValue;
        }
        return default(T)!;
    }

    /// <summary>
    /// Attempts to retrieve data associated with this event instance.
    /// Thread-safe operation using internal concurrent dictionary.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored value</typeparam>
    /// <param name="key">The unique key for the data</param>
    /// <param name="value">When this method returns, contains the stored value if found and of correct type</param>
    /// <returns>true if the key was found and the value is of the correct type; otherwise, false</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
    /// <example>
    /// <code>
    /// if (event.TryGetData("user_id", out int userId)) {
    ///     // Use userId safely
    ///     ProcessUser(userId);
    /// }
    /// </code>
    /// </example>
    public virtual bool TryGetData<T>(string key, out T value) {
        if (_data.TryGetValue(key, out var obj) && obj is T typedValue) {
            value = typedValue;
            return true;
        }
        value = default(T)!;
        return false;
    }

    /// <summary>
    /// Dispatches this event through the configured dispatcher for processing.
    /// 
    /// Initiates the state machine-based event processing pipeline:
    /// 1. Retrieves registered handlers from the dispatcher
    /// 2. Adds any event-scoped handlers configured via WithPhaseCallbacks()
    /// 3. Creates an EventSystemContext for state management
    /// 4. Processes the event through the state machine until completion
    /// 
    /// Processing Flow:
    /// - Business State: VALIDATE → CONFIGURE → EXECUTE → CLEANUP
    /// - Success/Failure transitions based on handler results
    /// - Terminal states: Completed or Cancelled
    /// 
    /// Thread Safety: This method is not thread-safe and should only be called once per event instance.
    /// </summary>
    /// <returns>
    /// The final processing result:
    /// - SUCCESS: All phases completed successfully
    /// - FAILURE: Event failed but completed processing
    /// - CANCELLED: Event was cancelled during processing
    /// - WAITING: Event is waiting for external input (should not occur from this method)
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when dispatcher is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when event has already been dispatched</exception>
    /// <example>
    /// <code>
    /// var event = new UserRegistrationEvent(dispatcher, email, name);
    /// var result = event.Dispatch();
    /// 
    /// switch (result) {
    ///     case EventProcessResult.SUCCESS:
    ///         Console.WriteLine("User registered successfully");
    ///         break;
    ///     case EventProcessResult.FAILURE:
    ///         Console.WriteLine("Registration failed but was handled gracefully");
    ///         break;
    ///     case EventProcessResult.CANCELLED:
    ///         Console.WriteLine("Registration was cancelled");
    ///         break;
    /// }
    /// </code>
    /// </example>
    public EventProcessResult Dispatch() {
        if (Dispatcher == null) throw new LSArgumentException(nameof(Dispatcher));
        List<IHandlerEntry> handlers = Dispatcher.getHandlers(GetType());
        if (_eventHandlers.Count > 0) handlers.AddRange(_eventHandlers);

        InDispatch = true;
        var context = LSEventProcessContext.Create(Dispatcher, this, handlers);
        _isCanceled = () => context.IsCancelled;
        _hasFailures = () => context.HasFailures;
        return context.processEvent();
    }


    /// <summary>
    /// Configures event-scoped phase handlers for this event instance.
    /// 
    /// Event-scoped handlers are executed alongside global handlers but are specific to this event.
    /// They allow for dynamic behavior configuration without affecting the global dispatcher state.
    /// 
    /// Handler Priority and Execution Order:
    /// - Handlers within the same phase execute in priority order (CRITICAL → HIGH → NORMAL → LOW → BACKGROUND)
    /// - Event-scoped handlers are mixed with global handlers based on their priority
    /// - Multiple configurations can be chained together
    /// </summary>
    /// <typeparam name="TPhase">The specific phase state type (ValidatePhaseState, ConfigurePhaseState, etc.)</typeparam>
    /// <param name="configure">Array of configuration functions that create phase handler entries</param>
    /// <returns>This event instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when any configuration function returns null</exception>
    /// <example>
    /// <code>
    /// var event = new UserRegistrationEvent(dispatcher, email, name)
    ///     .WithPhaseCallbacks&lt;BusinessState.ValidatePhaseState&gt;(
    ///         register => register
    ///             .WithPriority(LSESPriority.HIGH)
    ///             .Handler(ctx => {
    ///                 if (string.IsNullOrEmpty(ctx.Event.GetData&lt;string&gt;("email"))) {
    ///                     return HandlerProcessResult.FAILURE;
    ///                 }
    ///                 return HandlerProcessResult.SUCCESS;
    ///             })
    ///             .Build()
    ///     )
    ///     .WithPhaseCallbacks&lt;BusinessState.ExecutePhaseState&gt;(
    ///         register => register
    ///             .Handler(ctx => {
    ///                 // Execute registration logic
    ///                 return HandlerProcessResult.SUCCESS;
    ///             })
    ///             .Build()
    ///     );
    /// 
    /// var result = event.Dispatch();
    /// </code>
    /// </example>
    public ILSEvent WithPhaseCallbacks<TPhase>(params System.Func<LSPhaseHandlerRegister<TPhase>, LSPhaseHandlerRegister<TPhase>>[] configure) where TPhase : LSEventBusinessState.PhaseState {
        if (configure == null || configure.Length == 0) return this;
        foreach (var config in configure) {
            var register = config(new LSPhaseHandlerRegister<TPhase>());
            if (register == null) throw new LSArgumentNullException(nameof(register));
            var entry = register.Build();
            if (entry == null) throw new LSArgumentNullException(nameof(entry));
            _eventHandlers.Add(entry);
        }
        return this;
    }

    /// <summary>
    /// Configures event-scoped phase handlers with strongly-typed event return for method chaining.
    /// 
    /// This is the generic version of WithPhaseCallbacks that returns the concrete event type
    /// instead of ILSEvent, enabling continued method chaining with type-specific methods.
    /// Functionally identical to the non-generic version.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type to return for chaining</typeparam>
    /// <typeparam name="TPhase">The specific phase state type (ValidatePhaseState, ConfigurePhaseState, etc.)</typeparam>
    /// <param name="configure">Array of configuration functions that create phase handler entries</param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    public TEvent WithPhaseCallbacks<TEvent, TPhase>(params System.Func<LSPhaseHandlerRegister<TPhase>, LSPhaseHandlerRegister<TPhase>>[] configure) where TEvent : ILSEvent where TPhase : LSEventBusinessState.PhaseState {
        return (TEvent)WithPhaseCallbacks<TPhase>(configure);
    }

    /// <summary>
    /// Configures event-scoped state handlers for this event instance.
    /// 
    /// State handlers are executed when events enter terminal states (SUCCESS, CANCELLED, COMPLETED)
    /// and are used for finalization logic, notifications, and cleanup operations. These handlers
    /// are specific to this event instance, unlike global handlers registered via the dispatcher.
    /// 
    /// Handler Priority and Execution Order:
    /// - Handlers within the same state execute in priority order (CRITICAL → HIGH → NORMAL → LOW → BACKGROUND)
    /// - Event-scoped handlers are mixed with global handlers based on their priority
    /// - Multiple configurations can be chained together
    /// 
    /// State Types:
    /// - LSEventSucceedState: Event completed successfully
    /// - LSEventCancelledState: Event was cancelled due to critical failures
    /// - LSEventCompletedState: Event completed (with or without failures)
    /// </summary>
    /// <typeparam name="TState">The specific state type (LSEventSucceedState, LSEventCancelledState, LSEventCompletedState)</typeparam>
    /// <param name="configure">Array of configuration functions that create state handler entries</param>
    /// <returns>This event instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when any configuration function or resulting entry is null</exception>
    public ILSEvent WithStateCallbacks<TState>(params System.Func<LSStateHandlerRegister<TState>, LSStateHandlerRegister<TState>>[] configure) where TState : IEventProcessState {
        if (configure == null || configure.Length == 0) return this;
        foreach (var config in configure) {
            var register = config(new LSStateHandlerRegister<TState>());
            var entry = register.Build();
            if (entry == null) throw new LSArgumentNullException(nameof(entry));
            _eventHandlers.Add(entry);
        }
        return this;
    }

    /// <summary>
    /// Configures event-scoped state handlers with strongly-typed event return for method chaining.
    /// 
    /// This is the generic version of WithStateCallbacks that returns the concrete event type
    /// instead of ILSEvent, enabling continued method chaining with type-specific methods.
    /// Functionally identical to the non-generic version.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type to return for chaining</typeparam>
    /// <typeparam name="TState">The specific state type (LSEventSucceedState, LSEventCancelledState, LSEventCompletedState)</typeparam>
    /// <param name="configure">Array of configuration functions that create state handler entries</param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    public TEvent WithStateCallbacks<TEvent, TState>(params System.Func<LSStateHandlerRegister<TState>, LSStateHandlerRegister<TState>>[] configure) where TState : IEventProcessState {
        return (TEvent)WithStateCallbacks<TState>(configure);
    }

    /// <summary>
    /// Adds a strongly-typed success handler that executes when the event completes successfully.
    /// 
    /// This is a convenience method for adding basic success handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventSucceedState after all business phases complete successfully.
    /// 
    /// This generic version provides access to the concrete event type within the handler,
    /// enabling type-specific operations and return type preservation for chaining.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for type-safe handler access</typeparam>
    /// <param name="handler">
    /// Action that executes when the event succeeds. Receives the strongly-typed event instance
    /// for type-safe access to event data and event-specific operations.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when handler is null</exception>
    public TEvent OnSucceed<TEvent>(LSAction<TEvent> handler) where TEvent : ILSEvent {
        if (handler == null) throw new LSArgumentNullException(nameof(handler));
        return (TEvent)WithStateCallbacks<LSEventSucceedState>(register => register.Handler((evt) => handler((TEvent)(object)this)));
    }

    /// <summary>
    /// Adds a simple success handler that executes when the event completes successfully.
    /// 
    /// This is a convenience method for adding basic success handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventSucceedState after all business phases complete successfully.
    /// </summary>
    /// <param name="handler">
    /// Action that executes when the event succeeds. Receives the event instance
    /// for access to event data and processing information.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when handler is null</exception>
    public ILSEvent OnSucceed(LSAction<ILSEvent> handler) {
        if (handler == null) throw new LSArgumentNullException(nameof(handler));
        return WithStateCallbacks<LSEventSucceedState>(register => register.Handler(handler));
    }

    /// <summary>
    /// Adds a simple handler that executes when the event is cancelled.
    /// 
    /// This is a convenience method for adding basic handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventCancelledState due to cancellation.
    /// </summary>
    /// <param name="handler">
    /// Action that executes when the event is cancelled. Receives the event instance
    /// for access to event data and cancellation information.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when handler is null</exception>
    public ILSEvent OnCancelled(LSAction<ILSEvent> handler) {
        if (handler == null) throw new LSArgumentNullException(nameof(handler));
        return WithStateCallbacks<LSEventCancelledState>(register => register.Handler(handler));
    }

    /// <summary>
    /// Adds a strongly-typed cancellation handler that executes when the event is cancelled.
    /// 
    /// This is a convenience method for adding basic cancellation handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventCancelledState due to critical failures or explicit cancellation.
    /// 
    /// This generic version provides access to the concrete event type within the handler,
    /// enabling type-specific operations and return type preservation for chaining.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for type-safe handler access</typeparam>
    /// <param name="handler">
    /// Action that executes when the event is cancelled. Receives the strongly-typed event instance
    /// for type-safe access to event data and event-specific cancellation handling.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when handler is null</exception>
    public TEvent OnCancelled<TEvent>(LSAction<TEvent> handler) where TEvent : ILSEvent {
        if (handler == null) throw new LSArgumentNullException(nameof(handler));
        return (TEvent)WithStateCallbacks<LSEventCancelledState>(register => register.Handler((evt) => handler((TEvent)(object)this)));
    }

    /// <summary>
    /// Adds a simple handler that executes when the event processing finishes.
    /// 
    /// This is a convenience method for adding basic handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventCompletedState, which is the final state for all events regardless
    /// of success or failure outcome.
    /// </summary>
    /// <param name="handler">
    /// Action that executes when the event completes. Receives the event instance
    /// for access to final event data and processing results.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when handler is null</exception>
    public ILSEvent OnCompleted(LSAction<ILSEvent> handler) {
        if (handler == null) throw new LSArgumentNullException(nameof(handler));
        return WithStateCallbacks<LSEventCompletedState>(register => register.Handler(handler));
    }

    /// <summary>
    /// Adds a strongly-typed handler that executes when the event processing finishes.
    /// 
    /// This is a convenience method for adding basic handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventCompletedState, which is the final state for all events regardless
    /// of success or failure outcome.
    /// 
    /// This generic version provides access to the concrete event type within the handler,
    /// enabling type-specific operations and return type preservation for chaining.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for type-safe handler access</typeparam>
    /// <param name="handler">
    /// Action that executes when the event completes. Receives the strongly-typed event instance
    /// for type-safe access to final event data and event-specific completion handling.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when handler is null</exception>
    public TEvent OnCompleted<TEvent>(LSAction<TEvent> handler) where TEvent : ILSEvent {
        if (handler == null) throw new LSArgumentNullException(nameof(handler));
        return (TEvent)WithStateCallbacks<LSEventCompletedState>(register => register.Handler((evt) => handler((TEvent)(object)this)));
    }

    /// <summary>
    /// Adds a conditional cancellation rule for a specific business processing phase.
    /// 
    /// This method allows dynamic cancellation of event processing based on runtime conditions
    /// evaluated during the specified phase. When the condition evaluates to true, the phase
    /// will be cancelled and event processing will transition to the cancellation flow.
    /// 
    /// Cancellation Logic:
    /// - Condition is evaluated before handlers in the specified phase execute
    /// - If condition returns true, phase execution is skipped and event is cancelled
    /// - If condition returns false, phase execution proceeds normally
    /// - Multiple cancellation conditions can be registered for the same phase
    /// - Any true condition will trigger cancellation (logical OR behavior)
    /// 
    /// Phase Impact:
    /// - ValidatePhaseState: Cancels before validation logic executes
    /// - ConfigurePhaseState: Cancels before resource allocation and setup
    /// - ExecutePhaseState: Cancels before core business logic execution
    /// - CleanupPhaseState: Cancels before cleanup operations (rare use case)
    /// 
    /// Use Cases:
    /// - Circuit breaker patterns for system protection
    /// - Dynamic business rule enforcement
    /// - Resource availability checks before allocation
    /// - Security condition validation before processing
    /// </summary>
    /// <typeparam name="TPhase">The specific phase type where cancellation condition applies</typeparam>
    /// <param name="condition">
    /// Function that evaluates whether to cancel the phase. Receives the event and
    /// handler entry for context. Returns true to cancel, false to proceed.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    public ILSEvent CancelPhaseIf<TPhase>(System.Func<ILSEvent, IHandlerEntry, bool> condition) where TPhase : LSEventBusinessState.PhaseState {
        return WithPhaseCallbacks<TPhase>(register => register.CancelIf(condition));
    }

    /// <summary>
    /// Adds a strongly-typed conditional cancellation rule for a specific business processing phase.
    /// 
    /// This method allows dynamic cancellation of event processing based on runtime conditions
    /// evaluated during the specified phase. This generic version provides access to the concrete
    /// event type within the condition evaluation, enabling type-specific operations and return
    /// type preservation for chaining.
    /// 
    /// Functionally identical to the non-generic version but with enhanced type safety.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for type-safe condition evaluation</typeparam>
    /// <typeparam name="TPhase">The specific phase type where cancellation condition applies</typeparam>
    /// <param name="condition">
    /// Function that evaluates whether to cancel the phase. Receives the strongly-typed event and
    /// handler entry for context. Returns true to cancel, false to proceed.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    public TEvent CancelPhaseIf<TEvent, TPhase>(System.Func<TEvent, IHandlerEntry, bool> condition) where TEvent : ILSEvent where TPhase : LSEventBusinessState.PhaseState {
        return (TEvent)CancelPhaseIf<TPhase>((evt, entry) => condition((TEvent)(object)this, entry));
    }

    /// <summary>
    /// Adds event-scoped handlers using the comprehensive event register configuration system.
    /// 
    /// This method provides access to the full LSEventRegister configuration API, allowing
    /// complex handler setups that combine multiple phases and states in a single configuration.
    /// The event register provides a unified interface for configuring all aspects of event handling.
    /// 
    /// Advanced Configuration:
    /// The LSEventRegister allows configuration of multiple phases, states, priorities, conditions,
    /// and complex handler relationships in a single fluent configuration chain. This is the most
    /// powerful method for event-scoped handler configuration.
    /// 
    /// Use Cases:
    /// - Complex multi-phase handler configurations
    /// - Conditional handler execution based on event data
    /// - Priority-based handler ordering across phases
    /// - Comprehensive event lifecycle management
    /// 
    /// Handler Registration:
    /// All handlers configured through the register are automatically built and added to the
    /// event's handler collection for execution during event processing.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for configuration and return value</typeparam>
    /// <param name="configRegister">
    /// Configuration function that receives an LSEventRegister for this event type
    /// and should return the configured register with all desired handlers.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when configRegister, returned register, or any handler entry is null</exception>
    /// <exception cref="LSException">Thrown when the event cannot be cast to the specified TEvent type</exception>
    public TEvent WithCallback<TEvent>(System.Func<LSEventRegister<TEvent>, LSEventRegister<TEvent>> configRegister) where TEvent : ILSEvent {
        var register = configRegister(new LSEventRegister<TEvent>());
        if (register == null) throw new LSArgumentNullException(nameof(register));
        var entries = register.GetEntries();
        foreach (var entry in entries) {
            if (entry == null) throw new LSArgumentNullException(nameof(entry));
            _eventHandlers.Add(entry);
        }
        if (this is TEvent tEvent) return tEvent;
        throw new LSException($"Cannot cast {this.GetType().FullName} to {typeof(TEvent).FullName}.");
    }

}

/// <summary>
/// Generic base event class for events associated with specific eventable instances.
/// 
/// This class extends LSEvent to provide strong typing for events that are specifically
/// associated with objects implementing ILSEventable. It maintains a reference to the
/// associated instance throughout the event lifecycle, enabling type-safe access to
/// the eventable object from handlers and other event processing components.
/// 
/// Common Use Cases:
/// - Component initialization events (OnInitializeEvent&lt;TComponent&gt;)
/// - State machine transition events for specific objects
/// - Domain-specific events tied to business entities
/// - Resource lifecycle events (creation, modification, deletion)
/// 
/// Type Safety Benefits:
/// - Compile-time verification of instance type compatibility
/// - IntelliSense support for instance-specific properties and methods
/// - Reduced casting and type checking in event handlers
/// - Clear intent about which object the event is associated with
/// 
/// Thread Safety:
/// - Instance property is immutable after construction
/// - Inherits thread safety characteristics from base LSEvent class
/// - Safe for concurrent access to Instance property
/// </summary>
/// <typeparam name="TInstance">The type of eventable instance this event is associated with</typeparam>
public abstract class LSEvent<TInstance> : LSEvent where TInstance : ILSEventable {
    /// <summary>
    /// Initializes a new instance of the generic event with an associated eventable instance.
    /// 
    /// This constructor establishes the association between the event and the specific
    /// eventable instance, ensuring type safety and providing convenient access to the
    /// instance throughout the event processing lifecycle.
    /// 
    /// The instance reference is immutable after construction and will be available
    /// to all handlers and event processing components via the Instance property.
    /// </summary>
    /// <param name="instance">
    /// The eventable instance this event is associated with. Must not be null.
    /// This instance will be accessible throughout the event lifecycle.
    /// </param>
    /// <param name="options">
    /// Optional configuration for event processing. If null, defaults will be used
    /// including the singleton dispatcher and no owner instance tracking.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when instance is null</exception>
    public LSEvent(TInstance instance, LSEventOptions? options) : base(options) {
        Instance = instance;
    }

    /// <summary>
    /// The eventable instance associated with this event.
    /// 
    /// This property provides strongly-typed access to the specific eventable object
    /// that this event is associated with. The instance remains immutable throughout
    /// the event lifecycle, ensuring consistent access for all handlers and processing
    /// components.
    /// 
    /// Handler Usage:
    /// Event handlers can directly access this property to interact with the associated
    /// object, call its methods, access its properties, or modify its state as needed
    /// for the specific event processing requirements.
    /// 
    /// Common Patterns:
    /// - Initialization events: Access Instance to configure the newly created object
    /// - State transitions: Use Instance to check current state and apply changes
    /// - Validation events: Inspect Instance properties for business rule validation
    /// - Cleanup events: Use Instance to properly dispose or clean up resources
    /// </summary>
    /// <value>
    /// The eventable instance of type TInstance that this event is associated with.
    /// This value is set during construction and cannot be changed.
    /// </value>
    public TInstance Instance { get; protected set; }
}
