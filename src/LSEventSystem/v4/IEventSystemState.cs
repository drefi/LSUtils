using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// State interface for the event processing state machine in v4.
/// 
/// Implements the State pattern for clean state management in the LSEventSystem.
/// Each state represents a specific stage in event processing with well-defined
/// responsibilities and transition rules.
/// 
/// State Machine Architecture:
/// - BusinessState: Main processing state handling sequential phases
/// - SucceedState: Successful completion state
/// - CancelledState: Critical failure state  
/// - CompletedState: Final terminal state
/// 
/// Key Responsibilities:
/// - Process events according to state-specific logic
/// - Manage state transitions based on processing results
/// - Handle external control operations (Resume/Cancel/Fail)
/// - Track processing status and outcomes
/// 
/// State Lifecycle:
/// 1. State creation with event context
/// 2. Process() execution until completion or waiting
/// 3. Optional external control via Resume/Cancel/Fail
/// 4. Transition to next state or termination
/// 
/// Thread Safety:
/// - State implementations should handle concurrent access
/// - External control methods may be called from different threads
/// - State transitions must be atomic and consistent
/// 
/// Usage Pattern:
/// States are managed internally by the EventSystemContext and should
/// not be created or manipulated directly by client code. External
/// interaction occurs through the context's control methods.
/// </summary>
public interface IEventSystemState {

    /// <summary>
    /// The final result of state processing.
    /// 
    /// Indicates the outcome of state execution:
    /// - SUCCESS: State completed successfully
    /// - FAILURE: State completed with failures
    /// - CANCELLED: State was cancelled due to critical errors
    /// - WAITING: State is paused waiting for external input
    /// - UNKNOWN: State has not completed processing
    /// 
    /// Used by the state machine to determine next transitions
    /// and final event processing outcomes.
    /// </summary>
    StateProcessResult StateResult { get; }
    
    /// <summary>
    /// Indicates whether the state has encountered any failures during processing.
    /// 
    /// True when:
    /// - Handler failures occurred during processing
    /// - Business logic reported failure conditions
    /// - Recoverable errors were encountered
    /// 
    /// False when:
    /// - All operations completed successfully
    /// - State has not begun processing
    /// - Only warnings (not failures) occurred
    /// 
    /// Note: Events can complete successfully even with failures,
    /// as failures may be recoverable or non-critical.
    /// </summary>
    bool HasFailures { get; }
    
    /// <summary>
    /// Indicates whether the state has been cancelled.
    /// 
    /// True when:
    /// - Critical failures require immediate termination
    /// - External cancellation was requested
    /// - Security violations or system errors occurred
    /// 
    /// False when:
    /// - State is processing normally
    /// - Only recoverable failures occurred
    /// - State completed successfully
    /// 
    /// Cancellation typically leads to immediate state machine
    /// termination and transition to CancelledState.
    /// </summary>
    bool HasCancelled { get; }
    
    /// <summary>
    /// Processes the event in this state according to state-specific logic.
    /// 
    /// Main processing method that executes the state's business logic.
    /// Called by the state machine to advance event processing.
    /// 
    /// Processing Behavior:
    /// - Execute state-specific operations (handlers, phases, etc.)
    /// - Evaluate processing results and outcomes
    /// - Determine next state transition or completion
    /// - Handle errors and exceptional conditions
    /// 
    /// Return Value:
    /// - Next state to transition to (normal flow)
    /// - Null if processing is complete (terminal state)
    /// - This state if waiting for external input
    /// 
    /// Threading: Called sequentially by state machine, but state
    /// implementations should handle concurrent access if needed.
    /// </summary>
    /// <returns>Next state to transition to, or null if processing is complete</returns>
    IEventSystemState? Process();
    
    /// <summary>
    /// Resumes processing from a waiting state.
    /// 
    /// Called by external systems when a waiting state can continue processing.
    /// Only meaningful for states that can enter waiting conditions.
    /// 
    /// Resume Behavior:
    /// - Continue from where processing was paused
    /// - Re-evaluate waiting conditions
    /// - Advance to next processing step or state
    /// - Handle cases where resume is no longer needed
    /// 
    /// State Compatibility:
    /// - BusinessState: Resume paused phase processing
    /// - Terminal States: Typically no-op (return null)
    /// - Completed States: No effect on final result
    /// 
    /// Threading: May be called from external threads, implementations
    /// should handle concurrent access appropriately.
    /// </summary>
    /// <returns>Next state to transition to, or null if no transition needed</returns>
    IEventSystemState? Resume();

    /// <summary>
    /// Cancels processing and transitions to cancelled state.
    /// 
    /// Called by external systems to abort event processing due to
    /// critical conditions, timeouts, or user cancellation requests.
    /// 
    /// Cancellation Behavior:
    /// - Immediately halt current processing
    /// - Clean up resources and partial state
    /// - Transition to CancelledState for finalization
    /// - Set appropriate cancellation flags and results
    /// 
    /// Vs. Failure: Cancellation is more severe than failure and
    /// typically indicates the entire operation should be aborted
    /// rather than attempting recovery or continuation.
    /// 
    /// State Effects:
    /// - HasCancelled becomes true
    /// - StateResult set to CANCELLED
    /// - Processing cannot be resumed
    /// 
    /// Threading: May be called from external threads, implementations
    /// should handle concurrent access appropriately.
    /// </summary>
    /// <returns>Next state to transition to (typically CancelledState)</returns>
    IEventSystemState? Cancel();

    /// <summary>
    /// Marks the state as failed and handles failure processing.
    /// 
    /// Called by external systems to indicate that the current operation
    /// has failed and should be handled according to failure policies.
    /// 
    /// Failure Behavior:
    /// - Mark state as having failures
    /// - Continue processing if possible (unlike cancellation)
    /// - Apply failure handling logic specific to the state
    /// - May transition to failure-specific states or continue normally
    /// 
    /// Vs. Cancellation: Failure indicates a problem but may allow
    /// continued processing (e.g., cleanup phases still run), while
    /// cancellation requires immediate termination.
    /// 
    /// State Effects:
    /// - HasFailures becomes true
    /// - StateResult may be set to FAILURE
    /// - Processing may continue with failure context
    /// 
    /// Threading: May be called from external threads, implementations
    /// should handle concurrent access appropriately.
    /// </summary>
    /// <returns>Next state to transition to based on failure handling logic</returns>
    IEventSystemState? Fail();

}
