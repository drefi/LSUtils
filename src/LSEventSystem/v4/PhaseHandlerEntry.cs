using System;

namespace LSUtils.EventSystem;
/// <summary>
/// Handler entry for v4. Simplified structure with only priority ordering.
/// No registration order - only priority matters for execution sequence.
/// </summary>
public class PhaseHandlerEntry : IHandlerEntry {
    public System.Guid ID { get; } = System.Guid.NewGuid();
    /// <summary>
    /// The phase this handler executes in.
    /// </summary>
    public EventSystemPhase Phase { get; internal set; }

    /// <summary>
    /// Execution priority within the phase. Lower values execute first.
    /// Only ordering mechanism - no registration order.
    /// </summary>
    public LSESPriority Priority { get; internal set; }

    /// <summary>
    /// The handler function to execute.
    /// </summary>
    public Func<EventSystemContext, HandlerProcessResult> Handler { get; internal set; } = null!;

    /// <summary>
    /// Optional condition that must be met for this handler to execute.
    /// </summary>
    public Func<ILSEvent, IHandlerEntry, bool> Condition { get; internal set; } = (evt, entry) => true;

    /// <summary>
    /// Optional description for debugging/logging.
    /// </summary>
    public string? Description { get; internal set; }
    public int ExecutionCount { get; internal set; } = 0;
    public bool WaitingBlockExecution { get; internal set; } = false;
}
