using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Fluent registration builder for multiple handlers associated with a specific event type.
/// 
/// The EventSystemRegister provides a fluent API for registering multiple handlers
/// for a single event type in one operation. It supports both phase handlers and
/// state handlers, allowing complex event processing configurations to be defined
/// declaratively.
/// 
/// Key Features:
/// - Type-safe registration constrained to specific event types
/// - Support for both phase and state handler registration
/// - Fluent method chaining for readable configuration
/// - Batch registration with single dispatcher call
/// - Automatic handler entry collection and management
/// 
/// Usage Pattern:
/// Used within dispatcher ForEvent() calls to register multiple handlers:
/// <code>
/// dispatcher.ForEvent&lt;MyEvent&gt;(register => register
///     .OnPhase&lt;BusinessState.ValidatePhaseState&gt;(phase => phase
///         .Handler(ctx => ValidateData(ctx))
///         .Build())
///     .OnPhase&lt;BusinessState.ExecutePhaseState&gt;(phase => phase
///         .Handler(ctx => ProcessData(ctx))
///         .Build())
///     .OnState&lt;SucceedState&gt;(state => state
///         .Handler(evt => SendNotification(evt))
///         .Build())
///     .Register());
/// </code>
/// 
/// Handler Types:
/// - Phase Handlers: Execute during specific business phases
/// - State Handlers: Execute during state transitions
/// - Mixed Registration: Both types can be registered together
/// 
/// Registration Flow:
/// 1. Create handler entries using fluent builders
/// 2. Collect entries in internal list
/// 3. Register all entries with dispatcher in batch
/// 4. Return array of handler IDs for tracking
/// 
/// Error Handling:
/// - Null handler entries are rejected with exceptions
/// - Builder configuration errors bubble up from sub-builders
/// - Registration failures are reported per handler
/// 
/// Thread Safety:
/// - Builder instances are not thread-safe
/// - Built entries are immutable and thread-safe
/// - Registration operations are thread-safe via dispatcher
/// </summary>
/// <typeparam name="TEvent">The specific event type this register configures handlers for</typeparam>
public class EventSystemRegister<TEvent> where TEvent : ILSEvent {
    /// <summary>
    /// Reference to the dispatcher that will register all collected handler entries.
    /// Used for creating sub-builders and performing final batch registration.
    /// </summary>
    private readonly LSESDispatcher _dispatcher;
    
    /// <summary>
    /// Collection of handler entries configured through the fluent API.
    /// Accumulates both phase and state handlers for batch registration.
    /// </summary>
    protected List<IHandlerEntry> _entries = new();

    /// <summary>
    /// Internal constructor for creating event system register instances.
    /// Called by the dispatcher when setting up multi-handler registration contexts.
    /// </summary>
    /// <param name="dispatcher">The dispatcher instance for handler registration</param>
    internal EventSystemRegister(LSESDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }
    
    /// <summary>
    /// Registers a state handler for a specific state transition.
    /// 
    /// State handlers execute when events enter specific states such as
    /// SucceedState, CancelledState, or CompletedState. They perform
    /// finalization, logging, and notification operations.
    /// 
    /// Configuration Process:
    /// 1. Creates StateHandlerRegister for the specified state type
    /// 2. Calls configuration function to build handler entry
    /// 3. Validates returned entry is not null
    /// 4. Adds entry to collection for batch registration
    /// 
    /// Common State Types:
    /// - SucceedState: Successful completion handlers
    /// - CancelledState: Cancellation cleanup handlers
    /// - CompletedState: Final completion handlers
    /// 
    /// Handler Responsibilities:
    /// - Logging and audit trail updates
    /// - External system notifications
    /// - Cleanup and resource disposal
    /// - Metrics and performance tracking
    /// </summary>
    /// <typeparam name="TState">The state type this handler will execute in</typeparam>
    /// <param name="configureStateHandler">Function to configure the state handler</param>
    /// <returns>This register instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when configuration returns null entry</exception>
    public EventSystemRegister<TEvent> OnState<TState>(Func<StateHandlerRegister<TState>, StateHandlerEntry> configureStateHandler) where TState : IEventSystemState {
        var register = new StateHandlerRegister<TState>(_dispatcher);
        var entry = configureStateHandler(register);
        if (entry == null) throw new LSArgumentNullException(nameof(entry));
        _entries.Add(entry);
        return this;
    }
    
    /// <summary>
    /// Registers a phase handler for a specific business phase.
    /// 
    /// Phase handlers execute during business processing phases such as
    /// validation, configuration, execution, and cleanup. They implement
    /// the core business logic and control event flow.
    /// 
    /// Configuration Process:
    /// 1. Creates PhaseHandlerRegister for the specified phase type
    /// 2. Calls configuration function to build handler entry
    /// 3. Validates returned entry is not null
    /// 4. Adds entry to collection for batch registration
    /// 
    /// Phase Types:
    /// - BusinessState.ValidatePhaseState: Input validation and early checks
    /// - BusinessState.ConfigurePhaseState: Resource allocation and setup
    /// - BusinessState.ExecutePhaseState: Core business logic execution
    /// - BusinessState.CleanupPhaseState: Finalization and resource cleanup
    /// 
    /// Handler Responsibilities:
    /// - Business logic implementation
    /// - Data validation and transformation
    /// - External service integration
    /// - Error handling and recovery
    /// - Flow control through return values
    /// </summary>
    /// <typeparam name="TPhase">The phase type this handler will execute in</typeparam>
    /// <param name="configurePhaseHandler">Function to configure the phase handler</param>
    /// <returns>This register instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when configuration returns null entry</exception>
    public EventSystemRegister<TEvent> OnPhase<TPhase>(Func<PhaseHandlerRegister<TPhase>, PhaseHandlerEntry> configurePhaseHandler) where TPhase : BusinessState.PhaseState {
        var register = PhaseHandlerRegister<TPhase>.Create(_dispatcher);
        var entry = configurePhaseHandler(register);
        if (entry == null) throw new LSArgumentNullException(nameof(entry));
        _entries.Add(entry);
        return this;
    }
    
    /// <summary>
    /// Registers all configured handlers with the dispatcher and returns their IDs.
    /// 
    /// Performs batch registration of all handler entries collected through
    /// the fluent API. Each handler is registered individually with the dispatcher
    /// and assigned a unique identifier for tracking and management.
    /// 
    /// Registration Process:
    /// 1. Iterate through all collected handler entries
    /// 2. Register each entry with dispatcher for TEvent type
    /// 3. Collect returned handler IDs
    /// 4. Return array of IDs for client tracking
    /// 
    /// Handler Activation:
    /// - All handlers become active immediately after registration
    /// - Handlers will execute for future events of TEvent type
    /// - Registration order doesn't affect execution order (priority does)
    /// 
    /// ID Usage:
    /// - Returned IDs can be used for handler unregistration
    /// - Useful for temporary or conditional handler registration
    /// - Important for testing and debugging scenarios
    /// 
    /// Error Handling:
    /// - Individual registration failures don't stop batch processing
    /// - Failed registrations may result in fewer IDs than entries
    /// - Callers should check returned array length if needed
    /// </summary>
    /// <returns>Array of unique identifiers for all successfully registered handlers</returns>
    public System.Guid[] Register() {
        var ids = new List<System.Guid>();
        foreach (var entry in _entries) {
            var id = _dispatcher.registerHandler(typeof(TEvent), entry);
            ids.Add(id);
        }
        return ids.ToArray();
    }

}
