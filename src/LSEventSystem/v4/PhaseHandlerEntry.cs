using System;

namespace LSUtils.EventSystem;

public class PhaseHandlerEntry : IHandlerEntry {
    public System.Guid ID { get; } = System.Guid.NewGuid();
    //public EventSystemPhase Phase { get; internal set; }
    public System.Type? PhaseType { get; internal set; }
    public LSESPriority Priority { get; internal set; } = LSESPriority.NORMAL;
    public Func<EventSystemContext, HandlerProcessResult> Handler { get; internal set; } = null!;
    public Func<ILSEvent, IHandlerEntry, bool> Condition { get; internal set; } = (evt, entry) => true;
    public string? Description { get; internal set; }
    public int ExecutionCount { get; internal set; } = 0;
    public bool WaitingBlockExecution { get; internal set; } = false;
}
