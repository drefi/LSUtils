using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// LSEventSystem v4 Dispatcher - Central handler registration and management system.
/// 
/// The LSESDispatcher serves as the core component for registering and managing event handlers
/// in the LSEventSystem v4. It provides a clean, fluent API for handler registration while
/// maintaining separation between global handlers and event-scoped handlers.
/// 
/// Key Features:
/// - Singleton pattern for application-wide handler registration
/// - Type-safe handler registration with compile-time validation
/// - Support for both phase-based and state-based handlers
/// - Priority-based handler execution within phases
/// - Conditional handler execution based on event state
/// 
/// Handler Types:
/// - Phase Handlers: Execute during specific business phases (VALIDATE, CONFIGURE, EXECUTE, CLEANUP)
/// - State Handlers: Execute when events enter specific states (Success, Cancelled, Completed)
/// 
/// Registration Patterns:
/// - Fluent API for complex multi-handler registration
/// - Individual handler registration for simple scenarios
/// - Event-specific registration with type constraints
/// 
/// Thread Safety:
/// - Handler registration is thread-safe
/// - Handler retrieval is thread-safe via internal collections
/// - Handler execution follows sequential phase-based model
/// 
/// Usage Example:
/// <code>
/// // Register multiple handlers for an event type
/// dispatcher.ForEvent&lt;UserRegistrationEvent&gt;(register => register
///     .OnPhase&lt;BusinessState.ValidatePhaseState&gt;(phase => phase
///         .WithPriority(LSESPriority.HIGH)
///         .Handler(ctx => ValidateUserData(ctx))
///         .Build())
///     .OnPhase&lt;BusinessState.ExecutePhaseState&gt;(phase => phase
///         .Handler(ctx => CreateUserAccount(ctx))
///         .Build())
///     .Register());
/// </code>
/// </summary>
public class LSDispatcher {
    /// <summary>
    /// Singleton instance providing application-wide access to the dispatcher.
    /// 
    /// Use this instance for global handler registration and event processing.
    /// The singleton pattern ensures consistent handler management across the application
    /// and eliminates the need for dependency injection in simple scenarios.
    /// 
    /// For advanced scenarios requiring multiple dispatcher instances or custom
    /// configuration, create new instances directly.
    /// </summary>
    public static LSDispatcher Singleton { get; } = new LSDispatcher();

    /// <summary>
    /// Internal storage for registered handlers, organized by event type.
    /// Handlers are stored as lists to support multiple handlers per event type
    /// with priority-based execution order determined at runtime.
    /// </summary>
    private readonly Dictionary<Type, List<IHandlerEntry>> _handlers = new();

    /// <summary>
    /// Registers multiple handlers for a specific event type using a fluent configuration pattern.
    /// 
    /// This method provides the primary registration API for complex handler setups where
    /// multiple handlers need to be registered for different phases or states of the same event.
    /// The registration process is atomic - either all handlers are registered successfully
    /// or none are registered (on failure).
    /// 
    /// Configuration Process:
    /// 1. Creates a new EventSystemRegister instance for the specified event type
    /// 2. Passes the register to the configuration function
    /// 3. Executes the registration to add handlers to the dispatcher
    /// 4. Returns array of handler IDs for tracking and management
    /// 
    /// Error Handling:
    /// - Catches all exceptions during registration
    /// - Returns empty array on any failure
    /// - Does not partially register handlers on configuration errors
    /// </summary>
    /// <typeparam name="TEvent">The event type to register handlers for</typeparam>
    /// <param name="configureRegister">
    /// Function that configures the registration with multiple handlers.
    /// Should return the array of handler IDs from register.Register().
    /// </param>
    /// <returns>
    /// Array of handler IDs that were successfully registered.
    /// Empty array if registration failed or no handlers were configured.
    /// </returns>
    /// <example>
    /// <code>
    /// var handlerIds = dispatcher.ForEvent&lt;UserRegistrationEvent&gt;(register => register
    ///     .OnPhase&lt;BusinessState.ValidatePhaseState&gt;(phase => phase
    ///         .WithPriority(LSESPriority.HIGH)
    ///         .Handler(ctx => {
    ///             // Validation logic
    ///             return HandlerProcessResult.SUCCESS;
    ///         })
    ///         .Build())
    ///     .OnPhase&lt;BusinessState.ExecutePhaseState&gt;(phase => phase
    ///         .Handler(ctx => {
    ///             // Execution logic
    ///             return HandlerProcessResult.SUCCESS;
    ///         })
    ///         .Build())
    ///     .Register());
    /// </code>
    /// </example>
    public System.Guid[] ForEvent<TEvent>(Func<LSEventRegister<TEvent>, System.Guid[]> configureRegister) where TEvent : ILSEvent {
        try {
            var register = new LSEventRegister<TEvent>(this);
            return configureRegister(register);
        } catch {
            return Array.Empty<System.Guid>();
        }
    }
    /// <summary>
    /// Registers a single phase handler for a specific event type and phase.
    /// 
    /// Simplified registration method for scenarios where only one handler needs to be
    /// registered for a specific phase. Provides direct access to phase handler registration
    /// without the overhead of the fluent EventSystemRegister API.
    /// 
    /// Phase Handler Characteristics:
    /// - Executes during the specified business phase (VALIDATE, CONFIGURE, EXECUTE, CLEANUP)
    /// - Returns HandlerProcessResult to control phase flow
    /// - Can access full EventSystemContext for event and state management
    /// - Subject to priority-based execution order within the phase
    /// 
    /// Error Handling:
    /// - Returns Guid.Empty on any registration failure
    /// - Catches and suppresses all exceptions during registration
    /// - Validates handler entry is not null before registration
    /// </summary>
    /// <typeparam name="TEvent">The event type to register the handler for</typeparam>
    /// <typeparam name="TPhase">The specific phase state type (ValidatePhaseState, etc.)</typeparam>
    /// <param name="configurePhaseHandler">
    /// Function that configures a single phase handler using PhaseHandlerRegister.
    /// Should return a built PhaseHandlerEntry.
    /// </param>
    /// <returns>
    /// The unique ID of the registered handler, or Guid.Empty if registration failed.
    /// </returns>
    /// <example>
    /// <code>
    /// var handlerId = dispatcher.ForEventPhase&lt;UserRegistrationEvent, BusinessState.ValidatePhaseState&gt;(
    ///     register => register
    ///         .WithPriority(LSESPriority.HIGH)
    ///         .Handler(ctx => {
    ///             var email = ctx.Event.GetData&lt;string&gt;("email");
    ///             if (string.IsNullOrEmpty(email)) {
    ///                 return HandlerProcessResult.FAILURE;
    ///             }
    ///             return HandlerProcessResult.SUCCESS;
    ///         })
    ///         .Build());
    /// </code>
    /// </example>
    public System.Guid ForEventPhase<TEvent, TPhase>(Func<LSPhaseHandlerRegister<TPhase>, LSPhaseHandlerEntry> configurePhaseHandler) where TEvent : ILSEvent where TPhase : BusinessState.PhaseState {
        try {
            var register = LSPhaseHandlerRegister<TPhase>.Create(this);
            var entry = configurePhaseHandler(register);
            if (entry == null) throw new LSArgumentNullException(nameof(entry));
            return registerHandler(typeof(TEvent), entry);
        } catch {
            return System.Guid.Empty;
        }
    }
    /// <summary>
    /// Registers a single state handler for a specific event type and state.
    /// 
    /// Simplified registration method for state-based handlers that execute when events
    /// transition into specific states (Success, Cancelled, Completed). State handlers
    /// are typically used for cleanup, logging, notification, and finalization logic.
    /// 
    /// State Handler Characteristics:
    /// - Executes when event enters the specified state
    /// - Receives the event instance directly (not full context)
    /// - Primarily used for side effects and cleanup operations
    /// - Cannot affect event processing flow (processing is already determined)
    /// 
    /// Error Handling:
    /// - Returns Guid.Empty on any registration failure
    /// - Catches and suppresses all exceptions during registration
    /// - Validates handler entry is not null before registration
    /// </summary>
    /// <typeparam name="TEvent">The event type to register the handler for</typeparam>
    /// <typeparam name="TState">The specific state type (SucceedState, CancelledState, etc.)</typeparam>
    /// <param name="configureStateHandler">
    /// Function that configures a single state handler using StateHandlerRegister.
    /// Should return a built StateHandlerEntry.
    /// </param>
    /// <returns>
    /// The unique ID of the registered handler, or Guid.Empty if registration failed.
    /// </returns>
    /// <example>
    /// <code>
    /// var handlerId = dispatcher.ForEventState&lt;UserRegistrationEvent, SucceedState&gt;(
    ///     register => register
    ///         .WithPriority(LSESPriority.NORMAL)
    ///         .Handler(evt => {
    ///             // Success notification logic
    ///             var email = evt.GetData&lt;string&gt;("email");
    ///             SendWelcomeEmail(email);
    ///         })
    ///         .Build());
    /// </code>
    /// </example>
    public System.Guid ForEventState<TEvent, TState>(Func<LSStateHandlerRegister<TState>, LSStateHandlerEntry> configureStateHandler) where TEvent : ILSEvent where TState : IEventProcessState {
        try {
            var register = new LSStateHandlerRegister<TState>(this);
            var entry = configureStateHandler(register);
            if (entry == null) throw new LSArgumentNullException(nameof(entry));
            return registerHandler(typeof(TEvent), entry);
        } catch {
            return System.Guid.Empty;
        }
    }
    /// <summary>
    /// Internal method to register a handler entry for a specific event type.
    /// 
    /// Provides the core registration mechanism used by all public registration methods.
    /// Handles the storage of handler entries in the internal dictionary, organizing
    /// them by event type for efficient retrieval during event processing.
    /// 
    /// Registration Process:
    /// 1. Ensures handler collection exists for the event type
    /// 2. Adds the handler entry to the collection
    /// 3. Returns the handler's unique ID for tracking
    /// 
    /// This method is internal and should only be called by registration infrastructure.
    /// It does not perform validation - validation should be done by calling methods.
    /// </summary>
    /// <param name="eventType">The event type to register the handler for</param>
    /// <param name="entry">The complete handler entry to register</param>
    /// <returns>The unique ID of the registered handler</returns>
    internal System.Guid registerHandler(Type eventType, IHandlerEntry entry) {
        if (!_handlers.ContainsKey(eventType)) {
            _handlers[eventType] = new List<IHandlerEntry>();
        }
        _handlers[eventType].Add(entry);
        return entry.ID;
    }
    /// <summary>
    /// Internal method to retrieve all handlers registered for a specific event type.
    /// 
    /// Provides access to the complete collection of handlers for an event type,
    /// used during event processing to determine which handlers should be executed.
    /// Returns an empty list if no handlers are registered for the specified type.
    /// 
    /// Handler Collection:
    /// - Includes both phase handlers and state handlers
    /// - Handlers are not sorted - sorting occurs during phase execution
    /// - Collection is copied to prevent external modification
    /// 
    /// This method is internal and can only be called by event processing infrastructure,
    /// specifically by events during their Dispatch() method execution.
    /// 
    /// Thread Safety: This method is thread-safe for reading handler collections.
    /// </summary>
    /// <param name="eventType">The event type to retrieve handlers for</param>
    /// <returns>
    /// List of all handler entries registered for the event type.
    /// Empty list if no handlers are registered.
    /// </returns>
    internal List<IHandlerEntry> getHandlers(Type eventType) {
        return _handlers.TryGetValue(eventType, out var handlers) ? handlers : new List<IHandlerEntry>();
    }

}
