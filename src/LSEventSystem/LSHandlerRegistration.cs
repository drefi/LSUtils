using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Internal handler registration data that stores all information needed
/// to execute a registered handler. This class is used internally by the
/// dispatcher and should not be accessed by user code.
/// </summary>
internal class LSHandlerRegistration {
    /// <summary>
    /// Unique identifier for this handler registration.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// The event type this handler is registered for.
    /// </summary>
    public required Type EventType { get; set; }
    
    /// <summary>
    /// The handler function to execute, wrapped to work with the base event interface.
    /// </summary>
    public required Func<ILSEvent, LSPhaseContext, LSHandlerResult> Handler { get; set; }
    
    /// <summary>
    /// The phase in which this handler should execute.
    /// </summary>
    public LSEventPhase Phase { get; set; }
    
    /// <summary>
    /// The execution priority within the phase.
    /// </summary>
    public LSPhasePriority Priority { get; set; }
    
    /// <summary>
    /// The type of instance this handler is restricted to, if any.
    /// </summary>
    public Type? InstanceType { get; set; }
    
    /// <summary>
    /// The specific instance this handler is restricted to, if any.
    /// </summary>
    public object? Instance { get; set; }
    
    /// <summary>
    /// Maximum number of times this handler can execute. -1 means unlimited.
    /// </summary>
    public int MaxExecutions { get; set; }
    
    /// <summary>
    /// Optional condition that must be met for this handler to execute.
    /// </summary>
    public Func<ILSEvent, bool>? Condition { get; set; }
    
    /// <summary>
    /// Number of times this handler has been executed.
    /// </summary>
    public int ExecutionCount { get; set; }
}
