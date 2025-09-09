namespace LSUtils.EventSystem;

/// <summary>
/// Event system phases in v4. Only these phases exist for handler registration.
/// Clean, focused phases that execute sequentially in Business state.
/// </summary>
public enum EventSystemPhase {
    NONE = 0,
    /// <summary>
    /// Input validation, permission checks, early validation logic.
    /// First phase in Business state.
    /// </summary>
    VALIDATE = 1,
    
    /// <summary>
    /// Configuration, setup, resource allocation, state preparation.
    /// Second phase in Business state.
    /// </summary>
    CONFIGURE = 2,
    
    /// <summary>
    /// Core business logic and main event processing.
    /// Third phase in Business state.
    /// </summary>
    EXECUTE = 4,
    
    /// <summary>
    /// Cleanup, finalization, resource disposal.
    /// Final phase in Business state.
    /// </summary>
    CLEANUP = 8
}
