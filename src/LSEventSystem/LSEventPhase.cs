using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Defines the phases in the event processing lifecycle with strict execution order.
/// Each phase has a specific purpose and executes in predictable sequence.
/// 
/// Standard flow: VALIDATE → PREPARE → EXECUTE → SUCCESS → COMPLETE
/// Cancellation flow: [VALIDATE|PREPARE|EXECUTE] → CANCEL → COMPLETE
/// Failure flow: VALIDATE → PREPARE → EXECUTE → FAILURE → COMPLETE
/// 
/// Phase completion is tracked via CompletedPhases bitwise flags.
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
    /// Only runs when event completes successfully without cancellation or failure.
    /// </summary>
    SUCCESS = 8,

    /// <summary>
    /// Failure handling and recovery logic.
    /// Only runs when event has failures but isn't cancelled.
    /// </summary>
    FAILURE = 16,

    /// <summary>
    /// Cancellation handling and cleanup logic.
    /// Only runs when event is cancelled.
    /// </summary>
    CANCEL = 32,

    /// <summary>
    /// Always runs regardless of previous phase results.
    /// Use for logging, metrics, notifications, and guaranteed cleanup operations.
    /// </summary>
    COMPLETE = 64
}

/// <summary>
/// Execution priority within a phase. Lower numeric values execute first.
/// </summary>
public enum LSPhasePriority {
    /// <summary>
    /// System-critical operations such as validation and security checks.
    /// </summary>
    CRITICAL = 0,

    /// <summary>
    /// Important business logic that should run early in the phase.
    /// </summary>
    HIGH = 1,

    /// <summary>
    /// Standard operations and normal business logic. Default priority.
    /// </summary>
    NORMAL = 2,

    /// <summary>
    /// Nice-to-have features and optional functionality.
    /// </summary>
    LOW = 3,

    /// <summary>
    /// Background operations like logging, metrics, and non-critical cleanup.
    /// </summary>
    BACKGROUND = 4
}

/// <summary>
/// Result of phase handler execution that controls the flow of event processing.
/// </summary>
public enum LSPhaseResult {
    /// <summary>
    /// Continue to the next handler in the current phase, then proceed to the next phase.
    /// </summary>
    CONTINUE,

    /// <summary>
    /// Skip remaining handlers in this phase but continue to the next phase.
    /// </summary>
    SKIP_REMAINING,

    /// <summary>
    /// Stop execution completely and mark the event as cancelled.
    /// Only the CANCEL and COMPLETE phases will run.
    /// </summary>
    CANCEL,

    /// <summary>
    /// Immediately end the current phase and jump to the FAILURE phase, skipping SUCCESS.
    /// Use to explicitly signal a recoverable failure and trigger failure handling logic.
    /// </summary>
    FAILURE,

    /// <summary>
    /// Retry this handler execution (used with retry policies).
    /// </summary>
    RETRY,

    /// <summary>
    /// Pause event processing and wait for external signal to continue.
    /// Event will not proceed until Resume(), Abort(), or Fail() is called.
    /// </summary>
    WAITING
}
