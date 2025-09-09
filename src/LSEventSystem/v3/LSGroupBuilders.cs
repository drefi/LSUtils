using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Builder for sequential groups where order matters.
/// </summary>
public class LSSequentialGroupBuilder<TEvent> where TEvent : ILSEvent {
    private readonly LSDispatcher_v3 _dispatcher;
    private readonly LSEventPhase _phase;

    internal LSSequentialGroupBuilder(LSDispatcher_v3 dispatcher, LSEventPhase phase) {
        _dispatcher = dispatcher;
        _phase = phase;
    }

    /// <summary>
    /// Adds a handler to the sequential group.
    /// </summary>
    public LSSequentialGroupBuilder<TEvent> Handler(Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        registerHandler(handler, priority);
        return this;
    }

    /// <summary>
    /// Adds multiple handlers that will execute in sequence.
    /// </summary>
    public LSSequentialGroupBuilder<TEvent> Chain(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Handler(handler);
        }
        return this;
    }

    /// <summary>
    /// Adds a conditional handler to the group.
    /// </summary>
    public LSSequentialGroupBuilder<TEvent> When(Func<TEvent, bool> condition, Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        var entry = new LSHandlerEntry_v3 {
            Phase = _phase,
            Priority = priority,
            ExecutionMode = LSHandlerExecutionMode_v3.Sequential,
            Handler = evt => handler((TEvent)evt),
            Condition = evt => condition((TEvent)evt)
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
        return this;
    }

    /// <summary>
    /// Adds a conditional handler block to the group.
    /// </summary>
    public LSSequentialGroupBuilder<TEvent> When(Func<TEvent, bool> condition, Action<LSConditionalBuilder_v3<TEvent>> configureConditional) {
        var conditionalBuilder = new LSConditionalBuilder_v3<TEvent>(_dispatcher, _phase, condition, false);
        configureConditional(conditionalBuilder);
        return this;
    }

    private void registerHandler(Func<TEvent, LSHandlerResult> handler, LSESPriority priority) {
        var entry = new LSHandlerEntry_v3 {
            Phase = _phase,
            Priority = priority,
            ExecutionMode = LSHandlerExecutionMode_v3.Sequential,
            Handler = evt => handler((TEvent)evt)
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
    }
}

/// <summary>
/// Builder for parallel groups where order doesn't matter.
/// </summary>
public class LSParallelGroupBuilder<TEvent> where TEvent : ILSEvent {
    private readonly LSDispatcher_v3 _dispatcher;
    private readonly LSEventPhase _phase;

    internal LSParallelGroupBuilder(LSDispatcher_v3 dispatcher, LSEventPhase phase) {
        _dispatcher = dispatcher;
        _phase = phase;
    }

    /// <summary>
    /// Adds a handler to the parallel group.
    /// </summary>
    public LSParallelGroupBuilder<TEvent> Handler(Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        registerHandler(handler, priority);
        return this;
    }

    /// <summary>
    /// Adds multiple handlers that will execute in parallel (logically independent).
    /// </summary>
    public LSParallelGroupBuilder<TEvent> Concurrent(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Handler(handler);
        }
        return this;
    }

    /// <summary>
    /// Adds a conditional handler to the group.
    /// </summary>
    public LSParallelGroupBuilder<TEvent> When(Func<TEvent, bool> condition, Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        var entry = new LSHandlerEntry_v3 {
            Phase = _phase,
            Priority = priority,
            ExecutionMode = LSHandlerExecutionMode_v3.Parallel,
            Handler = evt => handler((TEvent)evt),
            Condition = evt => condition((TEvent)evt)
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
        return this;
    }

    private void registerHandler(Func<TEvent, LSHandlerResult> handler, LSESPriority priority) {
        var entry = new LSHandlerEntry_v3 {
            Phase = _phase,
            Priority = priority,
            ExecutionMode = LSHandlerExecutionMode_v3.Parallel,
            Handler = evt => handler((TEvent)evt)
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
    }
}

/// <summary>
/// Builder for priority-based groups.
/// </summary>
public class LSPriorityGroupBuilder<TEvent> where TEvent : ILSEvent {
    private readonly LSDispatcher_v3 _dispatcher;
    private readonly LSEventPhase _phase;
    private readonly LSESPriority _priority;

    internal LSPriorityGroupBuilder(LSDispatcher_v3 dispatcher, LSEventPhase phase, LSESPriority priority) {
        _dispatcher = dispatcher;
        _phase = phase;
        _priority = priority;
    }

    /// <summary>
    /// Adds a sequential handler to the priority group.
    /// </summary>
    public LSPriorityGroupBuilder<TEvent> Sequential(Func<TEvent, LSHandlerResult> handler) {
        registerHandler(handler, LSHandlerExecutionMode_v3.Sequential);
        return this;
    }

    /// <summary>
    /// Adds a parallel handler to the priority group.
    /// </summary>
    public LSPriorityGroupBuilder<TEvent> Parallel(Func<TEvent, LSHandlerResult> handler) {
        registerHandler(handler, LSHandlerExecutionMode_v3.Parallel);
        return this;
    }

    /// <summary>
    /// Adds a handler to the priority group (alias for sequential).
    /// </summary>
    public LSPriorityGroupBuilder<TEvent> Handler(Func<TEvent, LSHandlerResult> handler) {
        return Sequential(handler);
    }

    /// <summary>
    /// Adds multiple sequential handlers to the priority group.
    /// </summary>
    public LSPriorityGroupBuilder<TEvent> Chain(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Sequential(handler);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple parallel handlers to the priority group.
    /// </summary>
    public LSPriorityGroupBuilder<TEvent> Concurrent(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Parallel(handler);
        }
        return this;
    }

    private void registerHandler(Func<TEvent, LSHandlerResult> handler, LSHandlerExecutionMode_v3 mode) {
        var entry = new LSHandlerEntry_v3 {
            Phase = _phase,
            Priority = _priority,
            ExecutionMode = mode,
            Handler = evt => handler((TEvent)evt)
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
    }
}
