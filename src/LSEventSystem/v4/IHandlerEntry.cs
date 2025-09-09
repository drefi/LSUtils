using System;

namespace LSUtils.EventSystem;

public interface IHandlerEntry {
    System.Guid ID { get; }
    LSESPriority Priority { get; }
    Func<ILSEvent, IHandlerEntry, bool> Condition { get; }
    int ExecutionCount { get; }
}
