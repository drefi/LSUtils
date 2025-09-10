using System;
namespace LSUtils.EventSystem;

/// <summary>
/// Fluent registration builder for state-specific handlers in the LSEventSystem v4.
/// 
/// Provides a type-safe, fluent API for configuring and registering handlers that execute
/// when events enter specific states in the state machine. State handlers typically handle
/// finalization, success/failure logic, and cleanup operations.
/// 
/// Key Features:
/// - Type-safe state specification through generic constraints
/// - Fluent configuration API with method chaining
/// - Priority-based execution ordering within states
/// - Conditional execution based on event data
/// - Integration with dispatcher registration system
/// 
/// Usage Pattern:
/// The register is typically used within dispatcher registration calls:
/// <code>
/// dispatcher.ForEventState&lt;MyEvent, SucceedState&gt;(register => 
///     register
///         .WithPriority(LSESPriority.HIGH)
///         .When((evt, entry) => evt.GetData&lt;bool&gt;("sendNotification"))
///         .Handler(evt => {
///             // State-specific logic
///             SendSuccessNotification(evt);
///         })
///         .Build()
/// );
/// </code>
/// 
/// State Types:
/// - <see cref="SucceedState"/>: Successful event completion
/// - <see cref="CancelledState"/>: Event was cancelled due to critical failures
/// - <see cref="CompletedState"/>: Event completed (with or without failures)
/// 
/// Thread Safety:
/// - Builder instances are not thread-safe and should not be shared
/// - Built handler entries are immutable and thread-safe
/// - Registration operations are thread-safe via dispatcher
/// </summary>
/// <typeparam name="TState">The specific state type this handler will execute in</typeparam>
public class StateHandlerRegister<TState> where TState : IEventSystemState {
    /// <summary>
    /// Reference to the dispatcher that will register the built handler.
    /// Used for internal registration operations and handler management.
    /// </summary>
    private readonly LSESDispatcher _dispatcher;
    
    /// <summary>
    /// Priority level for handler execution within the state.
    /// Handlers execute in priority order: CRITICAL → HIGH → NORMAL → LOW → BACKGROUND.
    /// </summary>
    protected LSESPriority _priority = LSESPriority.NORMAL;
    
    /// <summary>
    /// The type of state this handler will execute in.
    /// Determined automatically from the generic type parameter TState.
    /// </summary>
    protected System.Type _stateType = typeof(TState);
    
    /// <summary>
    /// The handler action that will execute when the event enters this state.
    /// Takes the event as parameter for state-specific processing.
    /// </summary>
    protected LSAction<ILSEvent>? _handler = null;
    
    /// <summary>
    /// Condition function determining if the handler should execute.
    /// Evaluated at runtime based on event state and handler configuration.
    /// </summary>
    protected Func<ILSEvent, IHandlerEntry, bool> _condition = (evt, entry) => true;
    
    /// <summary>
    /// Indicates whether this builder has already been used to create a handler entry.
    /// Prevents multiple builds from the same register instance.
    /// </summary>
    public bool IsBuild { get; protected set; } = false;

    /// <summary>
    /// Internal constructor for creating state handler register instances.
    /// Called by the dispatcher when setting up handler registration contexts.
    /// </summary>
    /// <param name="dispatcher">The dispatcher instance that will handle registration</param>
    internal StateHandlerRegister(LSESDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }
    
    /// <summary>
    /// Sets the execution priority for this handler within its state.
    /// 
    /// Handlers execute in priority order within each state:
    /// - CRITICAL (0): System-critical operations, security cleanup
    /// - HIGH (1): Important finalization logic
    /// - NORMAL (2): Standard operations (default priority)
    /// - LOW (3): Optional processing, nice-to-have features
    /// - BACKGROUND (4): Logging, metrics, monitoring
    /// 
    /// State handlers typically run after business logic completion,
    /// so priority is mainly important for finalization order.
    /// </summary>
    /// <param name="priority">The priority level for handler execution</param>
    /// <returns>This register instance for method chaining</returns>
    public StateHandlerRegister<TState> WithPriority(LSESPriority priority) {
        _priority = priority;
        return this;
    }
    
    /// <summary>
    /// Adds a condition that must be met for the handler to execute.
    /// 
    /// Conditions are evaluated at runtime when the state processes handlers.
    /// Multiple conditions can be added and will be combined with logical AND.
    /// If any condition returns false, the handler is skipped.
    /// 
    /// Common Usage Patterns:
    /// - Success/failure checks: evt.GetData&lt;bool&gt;("operationSucceeded")
    /// - Data validation: evt.HasData("resultData")
    /// - Environment flags: evt.GetData&lt;string&gt;("environment") == "production"
    /// 
    /// Performance Note: Conditions should be lightweight as they're evaluated
    /// for every handler during state execution.
    /// </summary>
    /// <param name="condition">Function that returns true if handler should execute</param>
    /// <returns>This register instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when condition is null</exception>
    public StateHandlerRegister<TState> When(Func<ILSEvent, IHandlerEntry, bool> condition) {
        if (condition == null) throw new LSArgumentNullException(nameof(condition));
        _condition += condition;
        return this;
    }
    
    /// <summary>
    /// Sets the main handler action that executes when the event enters the state.
    /// 
    /// State handlers are simpler than phase handlers since they don't control
    /// flow - they perform finalization, logging, or cleanup operations after
    /// the main business logic has completed.
    /// 
    /// Handler Responsibilities:
    /// - Access event data for finalization logic
    /// - Perform cleanup or logging operations
    /// - Send notifications or trigger external systems
    /// - Update metrics or audit trails
    /// 
    /// Handler Limitations:
    /// - Cannot affect state transitions (state is already determined)
    /// - Should not throw exceptions (will be logged but not affect flow)
    /// - Should be relatively quick (other handlers are waiting)
    /// 
    /// Common Use Cases:
    /// - Success notifications in SucceedState
    /// - Error logging in CancelledState  
    /// - Audit trail updates in CompletedState
    /// - Resource cleanup in any terminal state
    /// </summary>
    /// <param name="handler">Action that implements the state-specific logic</param>
    /// <returns>This register instance for method chaining</returns>
    public StateHandlerRegister<TState> Handler(LSAction<ILSEvent> handler) {
        _handler = handler;
        return this;
    }
    /// <summary>
    /// Builds the configured handler into an immutable StateHandlerEntry.
    /// 
    /// Creates a handler entry with all configured properties (priority, condition,
    /// handler action, state type). The entry becomes immutable after creation
    /// and can be safely shared across threads.
    /// 
    /// Build Requirements:
    /// - Handler action must be set via Handler() method
    /// - State type is automatically determined from generic parameter
    /// - All other properties have sensible defaults
    /// 
    /// Post-Build State:
    /// - Register instance cannot be reused for different handlers
    /// - Configuration methods can still be called but have no effect
    /// - Entry contains immutable handler configuration
    /// </summary>
    /// <returns>Immutable handler entry ready for registration or execution</returns>
    /// <exception cref="LSException">Thrown when handler is not defined or already built</exception>
    public StateHandlerEntry Build() {
        if (_handler == null) throw new LSException("handler_not_defined");
        if (_stateType == null) throw new LSArgumentNullException(nameof(_stateType));
        if (IsBuild) throw new LSException("handler_already_built");
        var entry = new StateHandlerEntry {
            Priority = _priority,
            StateType = _stateType,
            Condition = _condition,
            Handler = _handler,
        };
        IsBuild = true;
        return entry;
    }
    
    /// <summary>
    /// Builds the handler and immediately registers it with the dispatcher.
    /// 
    /// Convenience method that combines Build() and dispatcher registration
    /// in a single operation. Useful for simple handler registration scenarios
    /// where you don't need to retain the handler entry reference.
    /// 
    /// Registration Process:
    /// 1. Validates handler configuration (calls Build() internally)
    /// 2. Registers handler with dispatcher for the TState type
    /// 3. Returns unique handler ID for tracking/unregistration
    /// 
    /// Handler Lifecycle:
    /// - Handler becomes active immediately after registration
    /// - Will execute for all future events that enter the associated state
    /// - Can be unregistered later using the returned ID
    /// 
    /// Error Handling:
    /// - Same validation as Build() method
    /// - Registration failures bubble up from dispatcher
    /// - Prevents multiple registrations from same register instance
    /// </summary>
    /// <returns>Unique identifier for the registered handler</returns>
    /// <exception cref="LSException">Thrown when handler has already been built</exception>
    public System.Guid Register() {
        if (IsBuild) throw new LSException("handler_already_built");
        var entry = Build();
        return _dispatcher.registerHandler(typeof(TState), entry);
    }

}
