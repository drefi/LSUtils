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
public class LSEventRegister<TEvent> where TEvent : ILSEvent_obsolete {
    /// <summary>
    /// Collection of handler entries configured through the fluent API.
    /// Accumulates both phase and state handlers for batch registration.
    /// </summary>
    protected List<IHandlerEntry> _entries = new();

    /// <summary>
    /// Internal constructor for creating event system register instances.
    /// Called by the dispatcher when setting up multi-handler registration contexts.
    /// </summary>
    public LSEventRegister() { }

    /// <summary>
    /// Create state handlers entries for a specific state transition.
    /// 
    /// State handlers execute when events enter specific states such as
    /// LSEventSucceedState, LSEventCancelledState, or LSEventCompletedState.
    /// 
    /// Configuration Process:
    /// 1. For each provided configuration function creates StateHandlerRegister for the specified state type
    /// 2. Calls configuration function to configure register
    /// 3. Builds the entry
    /// 4. Validates entry
    /// 5. Adds entry to collection for batch registration
    /// 
    /// Common State Types:
    /// - LSEventSucceedState: Successful handlers
    /// - LSEventCancelledState: Cancellation handlers
    /// - LSEventCompletedState: Final completion handlers
    /// </summary>
    /// <typeparam name="TState">The state type this handler will execute in</typeparam>
    /// <param name="configureStateHandler">Function to configure the state handler</param>
    /// <returns>This register instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when configuration returns null entry</exception>
    public LSEventRegister<TEvent> OnState<TState>(params Func<LSStateHandlerRegister<TEvent, TState>, LSStateHandlerRegister<TEvent, TState>>[] configureStateHandler) where TState : IEventProcessState {
        foreach (var handler in configureStateHandler) {
            var register = new LSStateHandlerRegister<TEvent, TState>();
            register = handler(register);
            if (register == null) throw new LSArgumentNullException(nameof(register));
            var entry = register.Build();
            _entries.Add(entry);
        }
        return this;
    }

    /// <summary>
    /// Create handlers for a specific business phase.
    /// 
    /// Phase handlers execute during business processing phases such as
    /// validation, configuration, execution, and cleanup. They implement
    /// the core business logic and control event flow.
    /// 
    /// Configuration Process:
    /// 1. For each provided configuration function creates PhaseHandlerRegister for the specified phase type
    /// 2. Calls configuration function to configure register
    /// 3. Builds the entry
    /// 4. Validates entry
    /// 5. Adds entry to collection for batch registration

    /// </summary>
    /// <typeparam name="TPhase">The phase type this handler will execute in</typeparam>
    /// <param name="configurePhaseHandler">Function to configure the phase handler</param>
    /// <returns>This register instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when configuration returns null entry</exception>
    public LSEventRegister<TEvent> OnPhase<TPhase>(params Func<LSPhaseHandlerRegister<TEvent, TPhase>, LSPhaseHandlerRegister<TEvent, TPhase>>[] configurePhaseHandler) where TPhase : LSEventBusinessState.PhaseState {
        foreach (var handler in configurePhaseHandler) {
            var register = new LSPhaseHandlerRegister<TEvent, TPhase>();
            register = handler(register);
            if (register == null) throw new LSArgumentNullException(nameof(register));
            var entry = register.Build();
            _entries.Add(entry);
        }
        return this;
    }
    /// <summary>
    /// Retrieves the list of all configured handler entries.
    /// </summary>
    public IReadOnlyList<IHandlerEntry> GetEntries() {
        return _entries.AsReadOnly();
    }
    /// <summary>
    /// Internal method to registers all configured handlers with the dispatcher and returns their IDs.
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
    internal System.Guid[] register(LSDispatcher dispatcher) {
        var ids = new List<System.Guid>();
        foreach (var entry in _entries) {
            var id = dispatcher.registerHandler(typeof(TEvent), entry);
            if (id == Guid.Empty) continue;
            ids.Add(id);
        }
        return ids.ToArray();
    }

}
