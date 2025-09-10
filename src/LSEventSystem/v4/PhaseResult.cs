namespace LSUtils.EventSystem;

/// <summary>
/// Result of phase execution in v4.
/// 
/// Controls transition between phases in the BusinessState and determines
/// the overall outcome of phase processing. Each phase evaluates handler
/// results to determine its final PhaseProcessResult.
/// 
/// Phase results guide the state machine's decision making:
/// - Which phase to execute next
/// - Whether to transition to a different state
/// - How to handle failures and waiting conditions
/// - When to terminate processing
/// </summary>
public enum PhaseProcessResult {
    /// <summary>
    /// Unknown or uninitialized phase result.
    /// Used internally for tracking and should not be the final result of phase processing.
    /// Indicates an error condition or incomplete phase execution.
    /// </summary>
    UNKNOWN,
    
    /// <summary>
    /// All handlers completed successfully, proceed to next phase.
    /// 
    /// Indicates that the phase executed successfully and processing should
    /// continue to the next phase in the sequence. This is the normal flow
    /// for successful phase completion.
    /// 
    /// Transition Behavior:
    /// - VALIDATE → CONFIGURE
    /// - CONFIGURE → EXECUTE  
    /// - EXECUTE → CLEANUP
    /// - CLEANUP → SucceedState (if no failures) or CompletedState
    /// </summary>
    CONTINUE,

    /// <summary>
    /// Phase failed but processing should continue through remaining phases.
    /// 
    /// Indicates that one or more handlers in the phase failed, but processing
    /// can continue. The event will be marked as having failures, which affects
    /// the final state transition (typically to failure handling rather than success).
    /// 
    /// Transition Behavior:
    /// - Continue to next phase in sequence
    /// - Final transition goes to CompletedState instead of SucceedState
    /// - Cleanup phase always executes even with failures
    /// </summary>
    FAILURE,

    /// <summary>
    /// Phase needs external action to continue - processing paused.
    /// 
    /// Indicates that one or more handlers in the phase returned WAITING,
    /// meaning external input is required before processing can continue.
    /// The event processing pauses at this phase until external actors
    /// call Resume(), Cancel(), or Fail() on the context.
    /// 
    /// Waiting Behavior:
    /// - Phase execution halts
    /// - Event remains in current state
    /// - External systems must resume processing
    /// - No timeout handling - external responsibility
    /// 
    /// Handler Responsibility:
    /// - Set up external callbacks to resume processing
    /// - Store operation identifiers in event data
    /// - Handle timeout scenarios externally
    /// </summary>
    WAITING,

    /// <summary>
    /// Phase was cancelled - immediate termination and transition to CancelledState.
    /// 
    /// Indicates that a critical failure occurred requiring immediate termination
    /// of processing. The event will transition to CancelledState for cleanup
    /// and finalization, bypassing remaining phases.
    /// 
    /// Cancellation Triggers:
    /// - Handler returned CANCELLED result
    /// - Critical validation failure in VALIDATE phase
    /// - Security violation or authentication failure
    /// - Unrecoverable system error
    /// 
    /// Transition Behavior:
    /// - Skip remaining phases in BusinessState
    /// - Transition directly to CancelledState
    /// - Execute cancellation cleanup handlers
    /// - Final transition to CompletedState
    /// </summary>
    CANCELLED,
}
