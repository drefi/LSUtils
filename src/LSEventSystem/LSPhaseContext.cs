using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Execution context provided to phase handlers during event processing.
/// Contains information about the current execution state, timing, and any errors.
/// This context is read-only from the handler perspective and provides observability
/// into the event processing pipeline.
/// </summary>
public class LSPhaseContext {
    /// <summary>
    /// The current phase being executed.
    /// </summary>
    public LSEventPhase CurrentPhase { get; }
    
    /// <summary>
    /// The priority level of the current handler within the phase.
    /// </summary>
    public LSESPriority Priority { get; }
    
    /// <summary>
    /// Total time elapsed since event processing started.
    /// Useful for performance monitoring and timeout detection.
    /// </summary>
    public TimeSpan ElapsedTime { get; }
    
    /// <summary>
    /// Number of handlers that have been executed in the current phase.
    /// Useful for understanding execution order and debugging.
    /// </summary>
    public int HandlersExecutedInPhase { get; }
    
    /// <summary>
    /// Indicates if any errors have occurred during event processing.
    /// </summary>
    public bool HasErrors { get; }
    
    /// <summary>
    /// Read-only list of error messages that have occurred during processing.
    /// Useful for error aggregation and debugging.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
    
    /// <summary>
    /// Initializes a new phase context with execution information.
    /// </summary>
    /// <param name="phase">The current phase being executed.</param>
    /// <param name="priority">The priority of the current handler.</param>
    /// <param name="elapsed">Time elapsed since event processing started.</param>
    /// <param name="executed">Number of handlers executed in the current phase.</param>
    /// <param name="errors">List of errors that have occurred during processing.</param>
    internal LSPhaseContext(LSEventPhase phase, LSESPriority priority, TimeSpan elapsed, int executed, List<string> errors) {
        CurrentPhase = phase;
        Priority = priority;
        ElapsedTime = elapsed;
        HandlersExecutedInPhase = executed;
        Errors = errors.AsReadOnly();
        HasErrors = errors.Count > 0;
    }
}
