using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Internal handler entry for event-scoped handlers in v3.
/// </summary>
internal class LSEventScopedHandler_v3 {
    public required LSEventPhase Phase { get; set; }
    public required LSESPriority Priority { get; set; }
    public required LSHandlerExecutionMode_v3 ExecutionMode { get; set; }
    public required Func<ILSEvent, LSHandlerResult> Handler { get; set; }
    public Func<ILSEvent, bool>? Condition { get; set; }
    
    /// <summary>
    /// Tracks the order in which event-scoped handlers were registered.
    /// </summary>
    public int RegistrationOrder { get; set; }
}
