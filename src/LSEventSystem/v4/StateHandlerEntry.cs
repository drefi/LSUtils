using System;

namespace LSUtils.EventSystem;

public class StateHandlerEntry : IHandlerEntry {
    public System.Guid ID { get; } = System.Guid.NewGuid();
    public System.Type? StateType { get; internal set; }
    public LSESPriority Priority { get; internal set; } = LSESPriority.NORMAL;
    public LSAction<ILSEvent> Handler { get; internal set; } = null!;
    public Func<ILSEvent, IHandlerEntry, bool> Condition { get; internal set; } = (evt, entry) => true;
    public int ExecutionCount { get; internal set; } = 0;
}
