namespace LSUtils;

/// <summary>
/// Defines the lifecycle phases of event processing, providing structured ordering
/// and conditional execution paths for listeners and callbacks.
/// </summary>
/// <remarks>
/// Event phases provide a predictable execution flow where certain phases are
/// always executed in order (DISPATCH → PRE_EXECUTION → EXECUTION → POST_EXECUTION),
/// followed by conditional phases based on the event's final state 
/// (SUCCESS | FAILURE | CANCEL), and finally the COMPLETE phase which always executes.
/// </remarks>
[System.Flags]
public enum EventPhase {
    /// <summary>
    /// Initial phase when event is dispatched to listeners.
    /// Used for immediate notifications and pre-processing setup.
    /// </summary>
    DISPATCH = 1 << 0,
    
    /// <summary>
    /// Pre-execution validation and setup phase.
    /// Used for input validation, security checks, and resource preparation.
    /// </summary>
    PRE_EXECUTION = 1 << 1,
    
    /// <summary>
    /// Main execution phase where core event processing occurs.
    /// This is the default phase for listeners that don't specify a phase.
    /// </summary>
    EXECUTION = 1 << 2,
    
    /// <summary>
    /// Post-execution cleanup and validation phase.
    /// Used for secondary effects, logging, and state validation.
    /// </summary>
    POST_EXECUTION = 1 << 3,
    
    /// <summary>
    /// Success-specific processing phase (conditional).
    /// Only executed if the event completed successfully without failures or cancellation.
    /// </summary>
    SUCCESS = 1 << 4,
    
    /// <summary>
    /// Failure-specific processing phase (conditional).
    /// Only executed if the event encountered a failure during processing.
    /// </summary>
    FAILURE = 1 << 5,
    
    /// <summary>
    /// Cancellation-specific processing phase (conditional).
    /// Only executed if the event was cancelled during processing.
    /// </summary>
    CANCEL = 1 << 6,
    
    /// <summary>
    /// Final completion phase, always executed.
    /// Used for final cleanup, resource disposal, and completion notifications.
    /// </summary>
    COMPLETE = 1 << 7,
    
    /// <summary>
    /// All phases combined for convenience.
    /// </summary>
    ALL = DISPATCH | PRE_EXECUTION | EXECUTION | POST_EXECUTION | SUCCESS | FAILURE | CANCEL | COMPLETE
}
