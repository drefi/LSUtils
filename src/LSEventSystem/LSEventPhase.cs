using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Clean phase-based event system phases that provide predictable execution flow 
/// with separation of concerns. Each phase has a specific purpose in the event 
/// processing lifecycle.
/// 
/// Phases execute in strict order: VALIDATE → PREPARE → EXECUTE → SUCCESS → COMPLETE
/// When cancelled: [VALIDATE|PREPARE|EXECUTE] → CANCEL → COMPLETE
/// 
/// **Phase Flow Rules:**
/// - If any phase (except COMPLETE) returns CANCEL, only CANCEL and COMPLETE phases will run
/// - SUCCESS phase only runs when event completes successfully (not cancelled)
/// - CANCEL phase only runs when event is cancelled
/// - COMPLETE phase always executes regardless of previous phase results
/// - Handlers within each phase execute in priority order
/// - Phase completion is tracked via CompletedPhases bitwise flags
/// 
/// **Typical Usage Patterns:**
/// - **VALIDATE**: Check permissions, validate input data, early exit conditions
/// - **PREPARE**: Load resources, acquire locks, setup temporary state
/// - **EXECUTE**: Core business logic, primary event processing
/// - **SUCCESS**: Save results, release resources, trigger success-specific side effects
/// - **CANCEL**: Rollback operations, cancel-specific cleanup, cancellation notifications
/// - **COMPLETE**: Logging, metrics, notifications, guaranteed cleanup
/// </summary>
public enum LSEventPhase {
    /// <summary>
    /// Input validation, permission checks, and early validation logic.
    /// Use for validating event data and checking if the event can proceed.
    /// </summary>
    VALIDATE = 1,

    /// <summary>
    /// Setup, resource allocation, and preparation logic.
    /// Use for preparing resources, setting up state, and initialization.
    /// </summary>
    PREPARE = 2,

    /// <summary>
    /// Core business logic and main event processing.
    /// Use for the primary event handling and business logic execution.
    /// </summary>
    EXECUTE = 4,

    /// <summary>
    /// Success handling, side effects, and post-processing logic.
    /// Only runs when event completes successfully (not cancelled).
    /// Use for success-specific cleanup, result processing, and side effects.
    /// </summary>
    SUCCESS = 8,

    /// <summary>
    /// Cancellation handling and cleanup logic.
    /// Only runs when event is cancelled. Use for cancel-specific cleanup, rollback operations,
    /// and cancellation notifications. Runs before COMPLETE phase.
    /// </summary>
    CANCEL = 16,

    /// <summary>
    /// Always runs regardless of previous phase results.
    /// Use for logging, metrics, notifications, and guaranteed cleanup operations.
    /// </summary>
    COMPLETE = 32
}

/// <summary>
/// Execution priority within a phase. Handlers with higher priority (lower numeric value)
/// are executed before handlers with lower priority (higher numeric value).
/// </summary>
public enum LSPhasePriority {
    /// <summary>
    /// System-critical operations such as validation and security checks.
    /// These handlers run first and their failure can abort the entire event.
    /// </summary>
    CRITICAL = 0,

    /// <summary>
    /// Important business logic that should run early in the phase.
    /// Use for core business operations that other handlers might depend on.
    /// </summary>
    HIGH = 1,

    /// <summary>
    /// Standard operations and normal business logic.
    /// This is the default priority for most handlers.
    /// </summary>
    NORMAL = 2,

    /// <summary>
    /// Nice-to-have features and optional functionality.
    /// Use for features that enhance the experience but aren't critical.
    /// </summary>
    LOW = 3,

    /// <summary>
    /// Background operations like logging, metrics, and non-critical cleanup.
    /// These handlers run last and their failure typically doesn't affect the event.
    /// </summary>
    BACKGROUND = 4
}

/// <summary>
/// Result of phase handler execution that controls the flow of event processing.
/// </summary>
public enum LSPhaseResult {
    /// <summary>
    /// Continue to the next handler in the current phase, then proceed to the next phase.
    /// This is the normal successful execution result.
    /// </summary>
    CONTINUE,

    /// <summary>
    /// Skip remaining handlers in this phase but continue to the next phase.
    /// Use when a handler has completed the work for the entire phase.
    /// </summary>
    SKIP_REMAINING,

    /// <summary>
    /// Stop execution completely and mark the event as cancelled.
    /// Only the COMPLETE phase will still run for cleanup purposes.
    /// </summary>
    CANCEL,

    /// <summary>
    /// Retry this handler execution (used with retry policies).
    /// The handler will be executed again up to the maximum retry limit.
    /// </summary>
    RETRY,

    /// <summary>
    /// Pause event processing and wait for external signal to continue.
    /// The event will not proceed to the next phase until ResumeProcessing() is called.
    /// Use for async operations that need to complete before continuing.
    /// Only the event itself can resume processing from this state.
    /// </summary>
    WAITING
}
