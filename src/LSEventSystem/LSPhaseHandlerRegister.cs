using System;
using System.Collections.Generic;
namespace LSUtils.EventSystem;

/// <summary>
/// Fluent registration builder for phase-specific handlers in the LSEventSystem v4.
/// 
/// Provides a type-safe, fluent API for configuring and registering handlers that execute
/// during specific business phases. Each handler is associated with a phase type and can
/// be configured with priority, conditions, and custom execution logic.
/// 
/// Key Features:
/// - Type-safe phase specification through generic constraints
/// - Fluent configuration API with method chaining
/// - Priority-based execution ordering
/// - Conditional execution based on event state
/// - Integration with dispatcher registration system
/// Phase Types:
/// - <see cref="LSEventBusinessState.ValidatePhaseState"/>: Input validation and early checks
/// - <see cref="LSEventBusinessState.ConfigurePhaseState"/>: Resource allocation and setup
/// - <see cref="LSEventBusinessState.ExecutePhaseState"/>: Core business logic execution
/// - <see cref="LSEventBusinessState.CleanupPhaseState"/>: Finalization and resource cleanup
/// 
/// Thread Safety:
/// - Builder instances are not thread-safe and should not be shared
/// - Built handler entries are immutable and thread-safe
/// - Registration operations are thread-safe via dispatcher
/// </summary>
/// <typeparam name="TPhase">The specific phase state type this handler will execute in</typeparam>
public class LSPhaseHandlerRegister<TEvent, TPhase> where TPhase : LSEventBusinessState.PhaseState where TEvent : ILSEvent_obsolete {

    /// <summary>
    /// The type of phase this handler will execute in.
    /// Determined automatically from the generic type parameter TPhase.
    /// </summary>
    protected System.Type _phaseType = typeof(TPhase);

    /// <summary>
    /// The handler function that will execute during the phase.
    /// Takes EventSystemContext and returns HandlerProcessResult to control flow.
    /// </summary>
    protected Func<LSEventProcessContext_Legacy, HandlerProcessResult>? _handler = null;

    /// <summary>
    /// Priority level for handler execution within the phase.
    /// Handlers execute in priority order: CRITICAL → HIGH → NORMAL → LOW → BACKGROUND.
    /// </summary>
    protected LSPriority _priority = LSPriority.NORMAL;

    /// <summary>
    /// Condition function determining if the handler should execute.
    /// Evaluated at runtime based on event state and handler configuration.
    /// </summary>
    protected Func<ILSEvent_obsolete, IHandlerEntry, bool> _condition = (evt, entry) => true;

    /// <summary>
    /// Indicates whether this builder has already been used to create a handler entry.
    /// Prevents multiple builds from the same register instance.
    /// </summary>
    public bool IsBuild { get; protected set; } = false;

    /// <summary>
    /// Cached handler entry created by the Build() method.
    /// Null until Build() is called, then contains the immutable handler configuration.
    /// </summary>
    protected LSPhaseHandlerEntry? _entry = null;

    /// <summary>
    /// Internal constructor for creating phase handler register instances.
    /// Called by the dispatcher when setting up handler registration contexts.
    /// </summary>
    /// <param name="dispatcher">The dispatcher instance that will handle registration</param>
    internal LSPhaseHandlerRegister() { }

    /// <summary>
    /// Sets the execution priority for this handler within its phase.
    /// 
    /// Handlers execute in priority order within each phase:
    /// - CRITICAL (0): System-critical operations, security checks
    /// - HIGH (1): Important business logic that must execute early
    /// - NORMAL (2): Standard operations (default priority)
    /// - LOW (3): Nice-to-have features, optional processing
    /// - BACKGROUND (4): Logging, metrics, cleanup operations
    /// 
    /// Priority affects execution order but not phase transitions.
    /// All handlers in a phase execute regardless of priority.
    /// </summary>
    /// <param name="priority">The priority level for handler execution</param>
    /// <returns>This register instance for method chaining</returns>
    public LSPhaseHandlerRegister<TEvent, TPhase> WithPriority(LSPriority priority) {
        _priority = priority;
        return this;
    }

    /// <summary>
    /// Adds a condition that must be met for the handler to execute.
    /// 
    /// Conditions are evaluated at runtime when the phase processes handlers.
    /// Multiple conditions can be added and will be combined with logical AND.
    /// If any condition returns false, the handler is skipped.
    /// 
    /// Common Usage Patterns:
    /// - Event data validation: evt.GetData&lt;bool&gt;("processFlag")
    /// - Feature flags: evt.GetData&lt;string&gt;("environment") == "production"
    /// - State checks: evt.HasData("userId")
    /// 
    /// Performance Note: Conditions should be lightweight as they're evaluated
    /// for every handler during phase execution.
    /// </summary>
    /// <param name="condition">Function that returns true if handler should execute</param>
    /// <returns>This register instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when condition is null</exception>
    public LSPhaseHandlerRegister<TEvent, TPhase> When(Func<TEvent, IHandlerEntry, bool> condition) {
        if (condition == null) throw new LSArgumentNullException(nameof(condition));
        _condition += new Func<ILSEvent_obsolete, IHandlerEntry, bool>((evt, entry) => condition((TEvent)evt, entry));
        return this;
    }

    /// <summary>
    /// Sets the main handler function that executes during the phase.
    /// 
    /// The handler function receives the EventSystemContext and must return a
    /// HandlerProcessResult to indicate the outcome of its execution:
    /// 
    /// - SUCCESS: Handler completed successfully, continue processing
    /// - FAILURE: Handler failed but processing can continue
    /// - WAITING: Handler needs external input, pause phase execution
    /// - CANCELLED: Critical failure, immediately transition to cancelled state
    /// 
    /// Handler Responsibilities:
    /// - Access event data via ctx.Event.GetData&lt;T&gt;()
    /// - Modify event data via ctx.Event.SetData()
    /// - Return appropriate result based on execution outcome
    /// - Handle exceptions gracefully
    /// 
    /// Context Access:
    /// - ctx.Event: The event being processed
    /// - ctx.Dispatcher: Handler registration system
    /// - ctx.Handlers: All handlers for this event
    /// 
    /// Threading: Handlers execute sequentially within phases.
    /// No special thread safety is required for handler logic.
    /// </summary>
    /// <param name="handler">Function that implements the phase-specific logic</param>
    /// <returns>This register instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when handler is null</exception>
    public LSPhaseHandlerRegister<TEvent, TPhase> Handler(Func<LSEventProcessContext_Legacy, HandlerProcessResult> handler) {
        _handler = handler;
        return this;
    }
    /// <summary>
    /// Builds the configured handler into an immutable PhaseHandlerEntry.
    /// 
    /// Creates a handler entry with all configured properties (priority, condition,
    /// handler function, phase type). The entry becomes immutable after creation
    /// and can be safely shared across threads.
    /// 
    /// Build Requirements:
    /// - Handler function must be set via Handler() method
    /// - Phase type is automatically determined from generic parameter
    /// - All other properties have sensible defaults
    /// 
    /// Caching Behavior:
    /// - Multiple calls return the same handler entry instance
    /// - Subsequent configuration changes after Build() are ignored
    /// - IsBuild property tracks whether building has occurred
    /// 
    /// Post-Build State:
    /// - Register instance cannot be reused for different handlers
    /// - Configuration methods can still be called but have no effect
    /// - Entry contains immutable handler configuration
    /// </summary>
    /// <returns>Immutable handler entry ready for registration or execution</returns>
    /// <exception cref="LSArgumentNullException">Thrown when handler function is not set</exception>
    /// <exception cref="LSException">Thrown when phase type is invalid</exception>
    public LSPhaseHandlerEntry Build() {
        if (_handler == null) throw new LSArgumentNullException(nameof(_handler));
        if (_phaseType == null) throw new LSException("invalid_phase_none");
        if (IsBuild && _entry != null) return _entry;
        _entry = new LSPhaseHandlerEntry {
            //Phase = _phase,
            PhaseType = _phaseType,
            Priority = _priority,
            Handler = _handler,
            Condition = _condition
        };
        IsBuild = true;
        return _entry;
    }

    /// <summary>
    /// Convenience method to create a handler that cancels the event if a condition is met.
    /// This will override any previously set handler and create a new one that checks the condition.
    /// </summary>
    public LSPhaseHandlerRegister<TEvent, TPhase> CancelIf(Func<TEvent, IHandlerEntry, bool> condition) {
        if (condition == null) throw new LSArgumentNullException(nameof(condition));
        _condition = new Func<ILSEvent_obsolete, IHandlerEntry, bool>((evt, entry) => condition((TEvent)evt, entry));
        return Handler((ctx) => HandlerProcessResult.CANCELLED).WithPriority(LSPriority.CRITICAL);
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
    /// 2. Registers handler with dispatcher for the TEvent type
    /// 3. Returns unique handler ID for tracking/unregistration
    /// 
    /// Handler Lifecycle:
    /// - Handler becomes active immediately after registration
    /// - Will execute for all future events of the associated type
    /// - Can be unregistered later using the returned ID
    /// 
    /// Error Handling:
    /// - Same validation as Build() method
    /// - Registration failures bubble up from dispatcher
    /// - Prevents multiple registrations from same register instance
    /// </summary>
    /// <returns>Unique identifier for the registered handler</returns>
    /// <exception cref="LSException">Thrown when handler has already been built</exception>
    public System.Guid Register(LSDispatcher dispatcher) {
        if (IsBuild) throw new LSException("handler_already_built");
        var entry = Build();
        return dispatcher.registerHandler(typeof(TEvent), entry);
    }
}
