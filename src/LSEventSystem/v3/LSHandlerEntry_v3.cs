using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Execution mode for handlers.
/// </summary>
public enum LSHandlerExecutionMode_v3 {
    /// <summary>
    /// Handlers execute in strict order - each handler waits for the previous to complete.
    /// </summary>
    Sequential,
    
    /// <summary>
    /// Handlers don't depend on execution order - can be optimized for parallel execution later.
    /// For now, still executes sequentially but indicates independence.
    /// </summary>
    Parallel
}

/// <summary>
/// Internal handler entry with registration order tracking.
/// </summary>
internal class LSHandlerEntry_v3 {
    public required LSEventPhase Phase { get; set; }
    public required LSESPriority Priority { get; set; }
    public required LSHandlerExecutionMode_v3 ExecutionMode { get; set; }
    public required Func<ILSEvent, LSHandlerResult> Handler { get; set; }
    public Func<ILSEvent, bool>? Condition { get; set; }
    public string? Description { get; set; }
    public int Executions { get; set; }
    public int RegistrationOrder { get; set; }
        
    /// <summary>
    /// Indicates whether this handler is event-scoped (true) or global (false).
    /// </summary>
    public bool IsEventScoped { get; set; }
}
