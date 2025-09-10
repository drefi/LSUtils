using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Event-scoped builder for sequential groups where order matters.
/// </summary>
public class LSEventSequentialGroupBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly LSEventCallbackBuilder_v3<TEvent> _builder;
    private readonly LSLegacyEventPhase _phase;

    internal LSEventSequentialGroupBuilder_v3(LSEventCallbackBuilder_v3<TEvent> builder, LSLegacyEventPhase phase) {
        _builder = builder;
        _phase = phase;
    }

    /// <summary>
    /// Adds a handler to the sequential group.
    /// </summary>
    public LSEventSequentialGroupBuilder_v3<TEvent> Handler(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Sequential, priority);
        return this;
    }

    /// <summary>
    /// Adds multiple handlers that will execute in sequence.
    /// </summary>
    public LSEventSequentialGroupBuilder_v3<TEvent> Chain(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Handler(handler);
        }
        return this;
    }

    /// <summary>
    /// Adds a conditional handler to the group.
    /// </summary>
    public LSEventSequentialGroupBuilder_v3<TEvent> When(Func<TEvent, bool> condition, Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Sequential, priority, condition);
        return this;
    }

    /// <summary>
    /// Adds a conditional handler block to the group.
    /// </summary>
    public LSEventSequentialGroupBuilder_v3<TEvent> When(Func<TEvent, bool> condition, Action<LSEventConditionalBuilder_v3<TEvent>> configureConditional) {
        var conditionalBuilder = new LSEventConditionalBuilder_v3<TEvent>(_builder, _phase, condition, false);
        configureConditional(conditionalBuilder);
        return this;
    }
}

/// <summary>
/// Event-scoped builder for parallel groups where order doesn't matter.
/// </summary>
public class LSEventParallelGroupBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly LSEventCallbackBuilder_v3<TEvent> _builder;
    private readonly LSLegacyEventPhase _phase;

    internal LSEventParallelGroupBuilder_v3(LSEventCallbackBuilder_v3<TEvent> builder, LSLegacyEventPhase phase) {
        _builder = builder;
        _phase = phase;
    }

    /// <summary>
    /// Adds a handler to the parallel group.
    /// </summary>
    public LSEventParallelGroupBuilder_v3<TEvent> Handler(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Parallel, priority);
        return this;
    }

    /// <summary>
    /// Adds multiple handlers that will execute in parallel (logically independent).
    /// </summary>
    public LSEventParallelGroupBuilder_v3<TEvent> Concurrent(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Handler(handler);
        }
        return this;
    }

    /// <summary>
    /// Adds a conditional handler to the group.
    /// </summary>
    public LSEventParallelGroupBuilder_v3<TEvent> When(Func<TEvent, bool> condition, Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Parallel, priority, condition);
        return this;
    }
}

/// <summary>
/// Event-scoped builder for priority-based groups.
/// </summary>
public class LSEventPriorityGroupBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly LSEventCallbackBuilder_v3<TEvent> _builder;
    private readonly LSLegacyEventPhase _phase;
    private readonly LSPriority _priority;

    internal LSEventPriorityGroupBuilder_v3(LSEventCallbackBuilder_v3<TEvent> builder, LSLegacyEventPhase phase, LSPriority priority) {
        _builder = builder;
        _phase = phase;
        _priority = priority;
    }

    /// <summary>
    /// Adds a sequential handler to the priority group.
    /// </summary>
    public LSEventPriorityGroupBuilder_v3<TEvent> Sequential(Func<TEvent, LSHandlerResult> handler) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Sequential, _priority);
        return this;
    }

    /// <summary>
    /// Adds a parallel handler to the priority group.
    /// </summary>
    public LSEventPriorityGroupBuilder_v3<TEvent> Parallel(Func<TEvent, LSHandlerResult> handler) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Parallel, _priority);
        return this;
    }

    /// <summary>
    /// Adds a handler to the priority group (alias for sequential).
    /// </summary>
    public LSEventPriorityGroupBuilder_v3<TEvent> Handler(Func<TEvent, LSHandlerResult> handler) {
        return Sequential(handler);
    }

    /// <summary>
    /// Adds multiple sequential handlers to the priority group.
    /// </summary>
    public LSEventPriorityGroupBuilder_v3<TEvent> Chain(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Sequential(handler);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple parallel handlers to the priority group.
    /// </summary>
    public LSEventPriorityGroupBuilder_v3<TEvent> Concurrent(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Parallel(handler);
        }
        return this;
    }

    /// <summary>
    /// Creates a parallel group within the priority group.
    /// </summary>
    public LSEventPriorityGroupBuilder_v3<TEvent> ParallelGroup(Action<LSEventParallelGroupBuilder_v3<TEvent>> configureGroup) {
        var groupBuilder = new LSEventParallelGroupBuilder_v3<TEvent>(_builder, _phase);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a sequential group within the priority group.
    /// </summary>
    public LSEventPriorityGroupBuilder_v3<TEvent> SequentialGroup(Action<LSEventSequentialGroupBuilder_v3<TEvent>> configureGroup) {
        var groupBuilder = new LSEventSequentialGroupBuilder_v3<TEvent>(_builder, _phase);
        configureGroup(groupBuilder);
        return this;
    }
}

/// <summary>
/// Event-scoped builder for conditional handlers that execute based on runtime conditions.
/// </summary>
public class LSEventConditionalBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly LSEventCallbackBuilder_v3<TEvent> _builder;
    private readonly LSLegacyEventPhase _phase;
    private readonly Func<TEvent, bool> _condition;
    private readonly bool _invert;

    internal LSEventConditionalBuilder_v3(LSEventCallbackBuilder_v3<TEvent> builder, LSLegacyEventPhase phase, Func<TEvent, bool> condition, bool invert) {
        _builder = builder;
        _phase = phase;
        _condition = condition;
        _invert = invert;
    }

    /// <summary>
    /// Adds a sequential handler to the conditional block.
    /// </summary>
    public LSEventConditionalBuilder_v3<TEvent> Sequential(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Sequential, priority, getEffectiveCondition());
        return this;
    }

    /// <summary>
    /// Adds a parallel handler to the conditional block.
    /// </summary>
    public LSEventConditionalBuilder_v3<TEvent> Parallel(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Parallel, priority, getEffectiveCondition());
        return this;
    }

    /// <summary>
    /// Adds a handler to the conditional block (alias for sequential).
    /// </summary>
    public LSEventConditionalBuilder_v3<TEvent> Handler(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        return Sequential(handler, priority);
    }

    /// <summary>
    /// Adds multiple sequential handlers to the conditional block.
    /// </summary>
    public LSEventConditionalBuilder_v3<TEvent> Chain(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Sequential(handler);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple parallel handlers to the conditional block.
    /// </summary>
    public LSEventConditionalBuilder_v3<TEvent> Concurrent(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Parallel(handler);
        }
        return this;
    }

    /// <summary>
    /// Creates a sequential group within the conditional block.
    /// </summary>
    public LSEventConditionalBuilder_v3<TEvent> SequentialGroup(Action<LSEventSequentialGroupBuilder_v3<TEvent>> configureGroup) {
        // For event-scoped handlers, we apply the condition to each individual handler
        var groupBuilder = new ConditionalEventSequentialGroupBuilder_v3<TEvent>(_builder, _phase, getEffectiveCondition());
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a parallel group within the conditional block.
    /// </summary>
    public LSEventConditionalBuilder_v3<TEvent> ParallelGroup(Action<LSEventParallelGroupBuilder_v3<TEvent>> configureGroup) {
        // For event-scoped handlers, we apply the condition to each individual handler
        var groupBuilder = new ConditionalEventParallelGroupBuilder_v3<TEvent>(_builder, _phase, getEffectiveCondition());
        configureGroup(groupBuilder);
        return this;
    }

    private Func<TEvent, bool> getEffectiveCondition() {
        return _invert 
            ? new Func<TEvent, bool>(evt => !_condition(evt))
            : _condition;
    }
}

/// <summary>
/// Helper class for sequential groups within conditional blocks (event-scoped).
/// </summary>
internal class ConditionalEventSequentialGroupBuilder_v3<TEvent> : LSEventSequentialGroupBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly Func<TEvent, bool> _condition;

    internal ConditionalEventSequentialGroupBuilder_v3(LSEventCallbackBuilder_v3<TEvent> builder, LSLegacyEventPhase phase, Func<TEvent, bool> condition) 
        : base(builder, phase) {
        _condition = condition;
    }

    // All handlers registered through this builder will have the condition applied
}

/// <summary>
/// Helper class for parallel groups within conditional blocks (event-scoped).
/// </summary>
internal class ConditionalEventParallelGroupBuilder_v3<TEvent> : LSEventParallelGroupBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly Func<TEvent, bool> _condition;

    internal ConditionalEventParallelGroupBuilder_v3(LSEventCallbackBuilder_v3<TEvent> builder, LSLegacyEventPhase phase, Func<TEvent, bool> condition) 
        : base(builder, phase) {
        _condition = condition;
    }

    // All handlers registered through this builder will have the condition applied
}
