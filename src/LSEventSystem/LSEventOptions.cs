using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Configuration for events in the LSEventSystem
/// 
/// LSEventOptions serves as the central configuration point for event processing.
/// This class provides a clean, extensible way to configure event behavior through
/// callback registration and dependency injection.
/// 
/// Key Features:
/// - Centralized dispatcher management for event processing
/// - Event-scoped callback registration for instance-specific handlers
/// - Owner instance tracking for context-aware processing
/// - Fluent API for easy configuration chaining
/// - Internal callback storage for phase and state handlers
/// 
/// Handler Registration Types:
/// 1. Global handlers: Registered via Dispatcher.ForEvent*() methods - shared across all event instances
/// 2. Event-scoped handlers: Registered via LSEventOptions fluent API - specific to individual event instances
/// 
/// Configuration Patterns:
/// - Using LSDispatch.Singleton: new LSEventOptions()
/// - With custom dispatcher: new LSEventOptions(dispatcher)
/// - With Owner: new LSEventOptions(dispatcher, ownerInstance)
/// 
/// Thread Safety:
/// - Instance is immutable after construction
/// - Callback lists are modified during configuration only
/// - Safe for concurrent access after configuration
/// </summary>
public class LSEventOptions {
    /// <summary>
    /// The dispatcher instance used for event processing and handler retrieval.
    /// 
    /// This dispatcher will be used to:
    /// - Retrieve globally registered handlers for event types
    /// - Process events through the state machine
    /// - Coordinate phase and state transitions
    /// 
    /// If not provided in constructor, defaults to LSDispatcher.Singleton.
    /// </summary>
    public LSDispatcher Dispatcher { get; init; }
    internal readonly List<IHandlerEntry> _entries = new();
    public IReadOnlyList<IHandlerEntry> Entries => _entries.AsReadOnly();
    /// <summary>
    /// Internal collection of callbacks for success state handlers.
    /// These callbacks are applied when events transition to SucceedState.
    /// </summary>
    internal readonly List<System.Func<LSStateHandlerRegister<LSEventSucceedState>, LSStateHandlerRegister<LSEventSucceedState>>> _succeedCallback = new();

    /// <summary>
    /// Internal collection of callbacks for cancellation state handlers.
    /// These callbacks are applied when events transition to CancelledState.
    /// </summary>
    internal readonly List<System.Func<LSStateHandlerRegister<LSEventCancelledState>, LSStateHandlerRegister<LSEventCancelledState>>> _cancelledCallback = new();

    /// <summary>
    /// Internal collection of callbacks for completion state handlers.
    /// These callbacks are applied when events transition to CompletedState.
    /// </summary>
    internal readonly List<System.Func<LSStateHandlerRegister<LSEventCompletedState>, LSStateHandlerRegister<LSEventCompletedState>>> _completedCallback = new();

    /// <summary>
    /// Internal collection of callbacks for validation phase handlers.
    /// These callbacks are applied during the BusinessState.ValidatePhaseState phase.
    /// </summary>
    internal readonly List<System.Func<LSPhaseHandlerRegister<LSEventBusinessState.ValidatePhaseState>, LSPhaseHandlerRegister<LSEventBusinessState.ValidatePhaseState>>> _validatePhaseCallback = new();

    /// <summary>
    /// Internal collection of callbacks for configuration phase handlers.
    /// These callbacks are applied during the BusinessState.ConfigurePhaseState phase.
    /// </summary>
    internal readonly List<System.Func<LSPhaseHandlerRegister<LSEventBusinessState.ConfigurePhaseState>, LSPhaseHandlerRegister<LSEventBusinessState.ConfigurePhaseState>>> _configurePhaseCallback = new();

    /// <summary>
    /// Internal collection of callbacks for execution phase handlers.
    /// These callbacks are applied during the BusinessState.ExecutePhaseState phase.
    /// </summary>
    internal readonly List<System.Func<LSPhaseHandlerRegister<LSEventBusinessState.ExecutePhaseState>, LSPhaseHandlerRegister<LSEventBusinessState.ExecutePhaseState>>> _executePhaseCallback = new();

    /// <summary>
    /// Internal collection of callbacks for cleanup phase handlers.
    /// These callbacks are applied during the BusinessState.CleanupPhaseState phase.
    /// </summary>
    internal readonly List<System.Func<LSPhaseHandlerRegister<LSEventBusinessState.CleanupPhaseState>, LSPhaseHandlerRegister<LSEventBusinessState.CleanupPhaseState>>> _cleanupPhaseCallback = new();

    /// <summary>
    /// The object instance that owns or is associated with events created with these options.
    /// 
    /// Common use cases:
    /// - Component initialization: Pass the component being initialized
    /// - State management: Pass the state machine or controller
    /// - Context tracking: Pass any object that provides context for event processing
    /// 
    /// This value is accessible to event handlers through the LSEventOptions instance
    /// and can be used for context-aware processing and ownership validation.
    /// </summary>
    public object? OwnerInstance { get; protected set; }

    /// <summary>
    /// Initializes a new instance of LSEventOptions with the specified configuration.
    /// 
    /// This constructor provides the primary way to configure event processing options
    /// including dispatcher assignment and owner instance tracking. All other configuration
    /// is done through the fluent API methods.
    /// </summary>
    /// <param name="dispatcher">
    /// The dispatcher to use for event processing. If null, defaults to LSDispatcher.Singleton.
    /// The dispatcher handles global handler retrieval and event processing coordination.
    /// </param>
    /// <param name="ownerInstance">
    /// Optional object that owns or is associated with events created using these options.
    /// Commonly used for component initialization and context tracking.
    /// </param>
    public LSEventOptions(LSDispatcher? dispatcher = null, object? ownerInstance = null) {
        Dispatcher = dispatcher ?? LSDispatcher.Singleton;
        OwnerInstance = ownerInstance;
    }

    /// <summary>
    /// Adds a callback for success state handling on individual event instances.
    /// 
    /// This registers an event-scoped handler that will be executed when events
    /// created with these options transition to SucceedState. This is different
    /// from global handlers registered via Dispatcher.ForEventState() - these
    /// handlers are specific to events created with this options instance.
    /// 
    /// Use this for instance-specific success notifications, completion logging,
    /// and finalization tasks that should only apply to particular event instances.
    /// 
    /// Multiple success callbacks can be registered and will all be executed
    /// when the success state is reached.
    /// </summary>
    /// <param name="callback">
    /// Function that configures a success state handler register.
    /// Should return the configured register for execution.
    /// </param>
    /// <returns>This LSEventOptions instance for method chaining</returns>
    public LSEventOptions OnSuccess(System.Func<LSStateHandlerRegister<LSEventSucceedState>, LSStateHandlerRegister<LSEventSucceedState>> callback) {
        var register = callback(new LSStateHandlerRegister<LSEventSucceedState>());
        _entries.Add(register.Build());
        return this;
    }

    /// <summary>
    /// Adds a callback for cancellation state handling on individual event instances.
    /// 
    /// This registers an event-scoped handler that will be executed when events
    /// created with these options transition to CancelledState. This is different
    /// from global handlers registered via Dispatcher.ForEventState() - these
    /// handlers are specific to events created with this options instance.
    /// 
    /// Use this for instance-specific cleanup operations, error notifications,
    /// and rollback procedures that should only apply to particular event instances.
    /// 
    /// Multiple cancellation callbacks can be registered and will all be executed
    /// when the cancellation state is reached.
    /// </summary>
    /// <param name="callback">
    /// Function that configures a cancellation state handler register.
    /// Should return the configured register for execution.
    /// </param>
    /// <returns>This LSEventOptions instance for method chaining</returns>
    public LSEventOptions OnCancel(System.Func<LSStateHandlerRegister<LSEventCancelledState>, LSStateHandlerRegister<LSEventCancelledState>> callback) {
        var register = callback(new LSStateHandlerRegister<LSEventCancelledState>());
        _entries.Add(register.Build());
        return this;
    }

    /// <summary>
    /// Adds a callback for completion state handling on individual event instances.
    /// 
    /// This registers an event-scoped handler that will be executed when events
    /// created with these options transition to CompletedState, which is the final
    /// state for all events regardless of success or failure. This is different
    /// from global handlers registered via Dispatcher.ForEventState() - these
    /// handlers are specific to events created with this options instance.
    /// 
    /// Use this for instance-specific final cleanup, audit logging, and resource
    /// disposal that should only apply to particular event instances.
    /// 
    /// Multiple completion callbacks can be registered and will all be executed
    /// when the completion state is reached.
    /// </summary>
    /// <param name="callback">
    /// Function that configures a completion state handler register.
    /// Should return the configured register for execution.
    /// </param>
    /// <returns>This LSEventOptions instance for method chaining</returns>
    public LSEventOptions OnComplete(System.Func<LSStateHandlerRegister<LSEventCompletedState>, LSStateHandlerRegister<LSEventCompletedState>> callback) {
        var register = callback(new LSStateHandlerRegister<LSEventCompletedState>());
        _entries.Add(register.Build());
        return this;
    }

    /// <summary>
    /// Adds a callback for validation phase handling on individual event instances.
    /// 
    /// This registers an event-scoped handler that will be executed during the
    /// BusinessState.ValidatePhaseState phase for events created with these options.
    /// This is different from global handlers registered via Dispatcher.ForEventPhase() -
    /// these handlers are specific to events created with this options instance.
    /// 
    /// Use this for instance-specific input validation, security checks, and early
    /// error detection that should only apply to particular event instances.
    /// 
    /// Multiple validation callbacks can be registered and will all be executed
    /// during the validation phase according to their priority order.
    /// </summary>
    /// <param name="callback">
    /// Function that configures a validation phase handler register.
    /// Should return the configured register for execution.
    /// </param>
    /// <returns>This LSEventOptions instance for method chaining</returns>
    public LSEventOptions OnValidatePhase(System.Func<LSPhaseHandlerRegister<LSEventBusinessState.ValidatePhaseState>, LSPhaseHandlerRegister<LSEventBusinessState.ValidatePhaseState>> callback) {
        var register = callback(new LSPhaseHandlerRegister<LSEventBusinessState.ValidatePhaseState>());
        _entries.Add(register.Build());
        return this;
    }

    /// <summary>
    /// Adds a callback for configuration phase handling on individual event instances.
    /// 
    /// This registers an event-scoped handler that will be executed during the
    /// BusinessState.ConfigurePhaseState phase for events created with these options.
    /// This phase follows validation and prepares resources for business logic execution.
    /// This is different from global handlers registered via Dispatcher.ForEventPhase() -
    /// these handlers are specific to events created with this options instance.
    /// 
    /// Use this for instance-specific resource allocation, setup operations, and state
    /// preparation that should only apply to particular event instances.
    /// 
    /// Multiple configuration callbacks can be registered and will all be executed
    /// during the configuration phase according to their priority order.
    /// </summary>
    /// <param name="callback">
    /// Function that configures a configuration phase handler register.
    /// Should return the configured register for execution.
    /// </param>
    /// <returns>This LSEventOptions instance for method chaining</returns>
    public LSEventOptions OnConfigurePhase(System.Func<LSPhaseHandlerRegister<LSEventBusinessState.ConfigurePhaseState>, LSPhaseHandlerRegister<LSEventBusinessState.ConfigurePhaseState>> callback) {
        var register = callback(new LSPhaseHandlerRegister<LSEventBusinessState.ConfigurePhaseState>());
        _entries.Add(register.Build());
        return this;
    }

    /// <summary>
    /// Adds a callback for execution phase handling on individual event instances.
    /// 
    /// This registers an event-scoped handler that will be executed during the
    /// BusinessState.ExecutePhaseState phase for events created with these options.
    /// This is where the core business logic runs. This is different from global
    /// handlers registered via Dispatcher.ForEventPhase() - these handlers are
    /// specific to events created with this options instance.
    /// 
    /// Use this for instance-specific main processing tasks, business rule execution,
    /// and primary event handling logic that should only apply to particular event instances.
    /// 
    /// Multiple execution callbacks can be registered and will all be executed
    /// during the execution phase according to their priority order.
    /// </summary>
    /// <param name="callback">
    /// Function that configures an execution phase handler register.
    /// Should return the configured register for execution.
    /// </param>
    /// <returns>This LSEventOptions instance for method chaining</returns>
    public LSEventOptions OnExecutePhase(System.Func<LSPhaseHandlerRegister<LSEventBusinessState.ExecutePhaseState>, LSPhaseHandlerRegister<LSEventBusinessState.ExecutePhaseState>> callback) {
        var register = callback(new LSPhaseHandlerRegister<LSEventBusinessState.ExecutePhaseState>());
        _entries.Add(register.Build());
        return this;
    }

    /// <summary>
    /// Adds a callback for cleanup phase handling on individual event instances.
    /// 
    /// This registers an event-scoped handler that will be executed during the
    /// BusinessState.CleanupPhaseState phase for events created with these options.
    /// This is the final business phase for resource cleanup and finalization.
    /// This is different from global handlers registered via Dispatcher.ForEventPhase() -
    /// these handlers are specific to events created with this options instance.
    /// 
    /// Use this for instance-specific disposal operations, cache cleanup, and state
    /// finalization that should only apply to particular event instances.
    /// 
    /// Multiple cleanup callbacks can be registered and will all be executed
    /// during the cleanup phase according to their priority order.
    /// </summary>
    /// <param name="callback">
    /// Function that configures a cleanup phase handler register.
    /// Should return the configured register for execution.
    /// </param>
    /// <returns>This LSEventOptions instance for method chaining</returns>
    public LSEventOptions OnCleanupPhase(System.Func<LSPhaseHandlerRegister<LSEventBusinessState.CleanupPhaseState>, LSPhaseHandlerRegister<LSEventBusinessState.CleanupPhaseState>> callback) {
        var register = callback(new LSPhaseHandlerRegister<LSEventBusinessState.CleanupPhaseState>());
        _entries.Add(register.Build());
        return this;
    }
}
