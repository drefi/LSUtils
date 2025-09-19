using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Core event interface representing an immutable data container with comprehensive state tracking.
/// Events serve as data carriers throughout the event processing pipeline, providing access to 
/// event metadata, processing state information, and associated data while maintaining clean 
/// separation between event data and processing logic.
/// 
/// Events are designed to be self-contained units of information that can be processed through 
/// multiple phases without requiring external state management. All state properties are 
/// read-only from the handler perspective to ensure immutability.
/// </summary>
public interface ILSEvent_obsolete {
    /// <summary>
    /// Unique identifier for this event instance.
    /// Generated automatically when the event is created and remains constant throughout
    /// the event's entire lifecycle.
    /// </summary>
    System.Guid ID { get; }

    /// <summary>
    /// UTC timestamp when this event was created.
    /// Provides timing information for event processing analytics, debugging, and audit trails.
    /// </summary>
    System.DateTime CreatedAt { get; }

    /// <summary>
    /// Indicates whether the event processing was cancelled by a handler.
    /// When true, no further phase processing will occur.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Indicates whether the event has failures.
    /// When true, the event will proceed depending on the phase behaviour.
    /// </summary>
    bool HasFailures { get; }

    /// <summary>
    /// Indicates whether the event has completed processing through all phases.
    /// An event is considered completed when the event processing lifecycle is finished.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Indicates whether the event is currently being dispatched.
    /// Prevents execution of events outside the Dispatch() call.
    /// </summary>
    bool InDispatch { get; }

    /// <summary>
    /// Read-only access to event data stored as key-value pairs.
    /// Contains all custom data associated with the event, allowing handlers 
    /// to share information across the event processing pipeline.
    /// </summary>
    System.Collections.Generic.IReadOnlyDictionary<string, object> Data { get; }

    /// <summary>
    /// Associates data with this event using a string key.
    /// Allows handlers to store information that persists for the lifetime of the event
    /// and can be accessed by subsequent handlers in the processing pipeline.
    /// </summary>
    /// <param name="key">The unique key to store the data under.</param>
    /// <param name="value">The data value to store.</param>
    void SetData<T>(string key, T value);

    /// <summary>
    /// Retrieves strongly-typed data associated with this event.
    /// Provides type-safe access to stored event data with compile-time type checking.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified key is not found.</exception>
    /// <exception cref="InvalidCastException">Thrown when the stored data cannot be cast to the specified type.</exception>
    T GetData<T>(string key);

    /// <summary>
    /// Attempts to retrieve strongly-typed data associated with this event.
    /// Provides safe access to stored event data without throwing exceptions for missing keys or type mismatches.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <param name="value">When this method returns, contains the retrieved value if successful, or the default value for T if unsuccessful.</param>
    /// <returns>true if the data was found and successfully cast to the specified type; otherwise, false.</returns>
    bool TryGetData<T>(string key, out T value);

    /// <summary>
    /// Executes the event through the complete processing pipeline.
    /// 
    /// Initiates event processing through all configured phases and states according to the
    /// LSEventSystem state machine. The event will progress through validation, configuration,
    /// execution, and cleanup phases, with appropriate state transitions based on handler results.
    /// 
    /// Processing Flow:
    /// 1. VALIDATE phase - Input validation and early error detection
    /// 2. CONFIGURE phase - Resource allocation and setup operations  
    /// 3. EXECUTE phase - Core business logic execution
    /// 4. CLEANUP phase - Resource cleanup and finalization
    /// 5. State transitions - SUCCESS, CANCELLED, or COMPLETED based on results
    /// </summary>
    /// <returns>
    /// EventProcessResult indicating the final outcome of event processing.
    /// Contains success/failure status and any relevant processing information.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when event is already completed or currently being dispatched</exception>
    EventProcessResult Dispatch();
    
    /// <summary>
    /// Adds event-scoped state handlers that execute when the event transitions to specific states.
    /// 
    /// State handlers are invoked when events enter terminal states (SUCCESS, CANCELLED, COMPLETED)
    /// and are used for finalization logic, notifications, and cleanup operations. These handlers
    /// are specific to this event instance, unlike global handlers registered via the dispatcher.
    /// 
    /// Configuration Functions:
    /// Each configure function receives a new LSStateHandlerRegister instance and should
    /// return the configured register. The event will automatically build the entry and
    /// add the handlers during event processing.
    /// </summary>
    /// <typeparam name="TState">The specific state type (LSEventSucceedState, LSEventCancelledState, LSEventCompletedState)</typeparam>
    /// <param name="configure">
    /// Configuration functions that set up state handlers. Each function receives a
    /// LSStateHandlerRegister and should return the configured register.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    TEvent WithStateCallbacks<TEvent, TState>(params System.Func<LSStateHandlerRegister<TEvent, TState>, LSStateHandlerRegister<TEvent, TState>>[] configure) where TState : IEventProcessState where TEvent : ILSEvent_obsolete;

    /// <summary>
    /// Adds event-scoped phase handlers that execute during specific business processing phases.
    /// 
    /// Phase handlers implement the core business logic for events and execute during the
    /// BusinessState processing. They are executed in the order: VALIDATE → CONFIGURE → EXECUTE → CLEANUP.
    /// These handlers are specific to this event instance, unlike global handlers registered via the dispatcher.
    /// 
    /// Configuration Functions:
    /// Each configure function receives a new LSPhaseHandlerRegister instance and should
    /// return the configured register. The system will automatically build and register
    /// the handlers during event processing.
    /// </summary>
    /// <typeparam name="TPhase">The specific phase type (ValidatePhaseState, ConfigurePhaseState, etc.)</typeparam>
    /// <param name="configure">
    /// Configuration functions that set up phase handlers. Each function receives a
    /// LSPhaseHandlerRegister and should return the configured register.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    TEvent WithPhaseCallbacks<TEvent, TPhase>(params System.Func<LSPhaseHandlerRegister<TEvent, TPhase>, LSPhaseHandlerRegister<TEvent, TPhase>>[] configure) where TEvent : ILSEvent_obsolete where TPhase : LSEventBusinessState.PhaseState;

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
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for configuration and return value</typeparam>
    /// <param name="configRegister">
    /// Configuration function that receives an LSEventRegister for this event type
    /// and should return the configured register with all desired handlers.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    TEvent WithCallback<TEvent>(System.Func<LSEventRegister<TEvent>, LSEventRegister<TEvent>> configRegister) where TEvent : ILSEvent_obsolete;
    
    /// <summary>
    /// Adds a simple handler that executes when the event completes successfully.
    /// 
    /// This is a convenience method for adding basic success handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventSucceedState after all business phases complete successfully.
    /// 
    /// Handler Execution:
    /// - Executes during LSEventSucceedState transition
    /// - Receives the event instance for access to data and metadata
    /// - Cannot cancel or modify event processing flow
    /// - Intended for notifications, logging, and finalization tasks
    /// </summary>
    /// <param name="handler">
    /// Action that executes when the event succeeds. Receives the event instance
    /// for access to event data and processing information.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    ILSEvent_obsolete OnSucceed(LSAction<ILSEvent_obsolete> handler);
    
    /// <summary>
    /// Adds a strongly-typed handler that executes when the event completes successfully.
    /// 
    /// This is the generic version of OnSucceed that provides access to the concrete event type
    /// within the handler, enabling type-specific operations and return type preservation for chaining.
    /// Functionally identical to the non-generic version but with enhanced type safety.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for type-safe handler access</typeparam>
    /// <param name="handler">
    /// Action that executes when the event succeeds. Receives the strongly-typed event instance
    /// for type-safe access to event data and event-specific operations.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    TEvent OnSucceed<TEvent>(LSAction<TEvent> handler) where TEvent : ILSEvent_obsolete;
    
    /// <summary>
    /// Adds a simple handler that executes when the event is cancelled.
    /// 
    /// This is a convenience method for adding basic handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventCancelledState due to explicit cancellation.
    /// 
    /// Handler Execution:
    /// - Executes during LSEventCancelledState transition
    /// - Receives the event instance for access to data and error information
    /// - Cannot modify event processing flow.
    /// - Intended for error handling, cleanup, and rollback operations
    /// </summary>
    /// <param name="handler">
    /// Action that executes when the event is cancelled. Receives the event instance
    /// for access to event data and cancellation information.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    ILSEvent_obsolete OnCancelled(LSAction<ILSEvent_obsolete> handler);
    
    /// <summary>
    /// Adds a strongly-typed handler that executes when the event is cancelled.
    /// 
    /// This is the generic version of OnCancelled that provides access to the concrete event type
    /// within the handler, enabling type-specific operations and return type preservation for chaining.
    /// Functionally identical to the non-generic version but with enhanced type safety.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for type-safe handler access</typeparam>
    /// <param name="handler">
    /// Action that executes when the event is cancelled. Receives the strongly-typed event instance
    /// for type-safe access to event data and event-specific cancellation handling.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    TEvent OnCancelled<TEvent>(LSAction<TEvent> handler) where TEvent : ILSEvent_obsolete;
    
    /// <summary>
    /// Adds a simple handler that executes when the event processing finishes.
    /// 
    /// This is a convenience method for adding basic handlers without the complexity
    /// of the full register configuration API. The handler will execute when the event
    /// transitions to LSEventCompletedState, which is the final state for all events regardless
    /// of success or failure outcome.
    /// 
    /// Handler Execution:
    /// - Executes during LSEventCompletedState transition (always the final transition)
    /// - Receives the event instance for access to final processing results
    /// - Cannot modify event processing flow (completion is terminal)
    /// - Intended for final cleanup, logging, and resource disposal
    /// 
    /// Use Cases:
    /// - Final resource cleanup and disposal
    /// - Comprehensive audit logging of event lifecycle
    /// - Performance metrics collection and reporting
    /// - Cache invalidation and state synchronization
    /// - Final notifications regardless of success/failure
    /// </summary>
    /// <param name="handler">
    /// Action that executes when the event completes. Receives the event instance
    /// for access to final event data and processing results.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    ILSEvent_obsolete OnCompleted(LSAction<ILSEvent_obsolete> handler);
    
    /// <summary>
    /// Adds a strongly-typed handler that executes when the event processing finishes.
    /// 
    /// This is the generic version of OnCompleted that provides access to the concrete event type
    /// within the handler, enabling type-specific operations and return type preservation for chaining.
    /// Functionally identical to the non-generic version but with enhanced type safety.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for type-safe handler access</typeparam>
    /// <param name="handler">
    /// Action that executes when the event completes. Receives the strongly-typed event instance
    /// for type-safe access to final event data and event-specific completion handling.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    TEvent OnCompleted<TEvent>(LSAction<TEvent> handler) where TEvent : ILSEvent_obsolete;
    
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
    /// </summary>
    /// <typeparam name="TPhase">The specific phase type where cancellation condition applies</typeparam>
    /// <param name="condition">
    /// Function that evaluates whether to cancel the phase. Receives the event and
    /// handler entry for context. Returns true to cancel, false to proceed.
    /// </param>
    /// <returns>This event instance for method chaining</returns>
    ILSEvent_obsolete CancelPhaseIf<TPhase>(System.Func<ILSEvent_obsolete, IHandlerEntry, bool> condition) where TPhase : LSEventBusinessState.PhaseState;
    
    /// <summary>
    /// Adds a strongly-typed conditional cancellation rule for a specific business processing phase.
    /// 
    /// This is the generic version of CancelPhaseIf that provides access to the concrete event type
    /// within the condition evaluation, enabling type-specific operations and return type preservation for chaining.
    /// Functionally identical to the non-generic version but with enhanced type safety.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type for type-safe condition evaluation</typeparam>
    /// <typeparam name="TPhase">The specific phase type where cancellation condition applies</typeparam>
    /// <param name="condition">
    /// Function that evaluates whether to cancel the phase. Receives the strongly-typed event and
    /// handler entry for context. Returns true to cancel, false to proceed.
    /// </param>
    /// <returns>This event instance cast to TEvent for continued method chaining</returns>
    TEvent CancelPhaseIf<TEvent, TPhase>(System.Func<TEvent, IHandlerEntry, bool> condition) where TEvent : ILSEvent_obsolete where TPhase : LSEventBusinessState.PhaseState;
}
