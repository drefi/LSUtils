namespace LSUtils.EventSystem;

/// <summary>
/// Result of state processing in v4.
/// 
/// Indicates the outcome of state execution and determines state machine transitions.
/// Used by states to communicate their processing results to the EventSystemContext
/// for proper state machine flow control.
/// </summary>
public enum StateProcessResult {
    /// <summary>
    /// Unknown or uninitialized state result.
    /// Indicates an error condition or incomplete state processing.
    /// Should not be used as a final result from state processing.
    /// </summary>
    UNKNOWN,
    
    /// <summary>
    /// State processing completed successfully.
    /// 
    /// Indicates that all handlers in the state executed successfully
    /// and processing should continue to the next logical state or
    /// complete if this is a terminal state.
    /// 
    /// Used by:
    /// - BusinessState when all phases complete without issues
    /// - SucceedState when success handlers complete
    /// - CancelledState when cancellation cleanup completes
    /// - CompletedState when final cleanup completes
    /// </summary>
    SUCCESS,
    
    /// <summary>
    /// State processing encountered failures but completed.
    /// 
    /// Indicates that one or more handlers failed during state processing,
    /// but the state was able to complete its work. This typically triggers
    /// transition to failure handling states rather than success states.
    /// 
    /// Used by:
    /// - BusinessState when phases complete but have failures
    /// - States when non-critical errors occur during processing
    /// </summary>
    FAILURE,
    
    /// <summary>
    /// State is waiting for external input to continue processing.
    /// 
    /// Indicates that the state has paused processing and is waiting
    /// for external actors to provide input via Resume(), Cancel(), or Fail().
    /// The state machine remains in the current state until external action.
    /// 
    /// Used by:
    /// - BusinessState when a phase returns WAITING
    /// - Any state that initiates asynchronous operations
    /// 
    /// Important: No internal threading - external control required
    /// </summary>
    WAITING,
    
    /// <summary>
    /// State processing was cancelled - transition to cancellation handling.
    /// 
    /// Indicates that a critical failure occurred requiring immediate
    /// termination of normal processing. The state machine should transition
    /// to CancelledState for proper cleanup and finalization.
    /// 
    /// Used by:
    /// - BusinessState when a phase returns CANCELLED
    /// - Any state when critical errors require immediate termination
    /// - External cancellation requests
    /// </summary>
    CANCELLED,
}

/// <summary>
/// Final result of complete event processing.
/// 
/// Represents the overall outcome of an event's journey through the
/// state machine, returned by the Dispatch() method to indicate how
/// the event processing concluded.
/// </summary>
public enum EventProcessResult {
    /// <summary>
    /// Unknown or uninitialized processing result.
    /// Should not occur in normal operation - indicates an error condition.
    /// </summary>
    UNKNOWN,
    
    /// <summary>
    /// Event processing completed successfully.
    /// 
    /// All phases executed successfully and the event reached the
    /// SucceedState and CompletedState without failures or cancellation.
    /// This indicates the event achieved its intended outcome.
    /// </summary>
    SUCCESS,
    
    /// <summary>
    /// Event processing completed with failures.
    /// 
    /// The event completed its processing lifecycle but encountered
    /// one or more failures along the way. The event reached CompletedState
    /// but did not achieve full success. Failure details are available
    /// in the event data.
    /// </summary>
    FAILURE,
    
    /// <summary>
    /// Event processing was cancelled.
    /// 
    /// A critical failure occurred that required immediate termination
    /// of processing. The event was cleaned up through CancelledState
    /// and CompletedState. Cancellation details are available in event data.
    /// </summary>
    CANCELLED,
    
    /// <summary>
    /// Event processing is waiting for external input.
    /// 
    /// The event has paused processing and is waiting for external
    /// actors to provide input via the EventSystemContext methods.
    /// This result should only occur if Dispatch() is called on an
    /// event that was already in a waiting state.
    /// 
    /// External systems must call Resume(), Cancel(), or Fail()
    /// to continue processing.
    /// </summary>
    WAITING
}
