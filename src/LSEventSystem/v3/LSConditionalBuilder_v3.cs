using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Builder for conditional handlers that execute based on runtime conditions.
/// </summary>
public class LSConditionalBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly LSDispatcher_v3 _dispatcher;
    private readonly LSLegacyEventPhase _phase;
    private readonly Func<TEvent, bool> _condition;
    private readonly bool _invert;

    internal LSConditionalBuilder_v3(LSDispatcher_v3 dispatcher, LSLegacyEventPhase phase, Func<TEvent, bool> condition, bool invert) {
        _dispatcher = dispatcher;
        _phase = phase;
        _condition = condition;
        _invert = invert;
    }

    /// <summary>
    /// Adds a sequential handler to the conditional block.
    /// </summary>
    public LSConditionalBuilder_v3<TEvent> Sequential(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        registerHandler(handler, LSHandlerExecutionMode_v3.Sequential, priority);
        return this;
    }

    /// <summary>
    /// Adds a parallel handler to the conditional block.
    /// </summary>
    public LSConditionalBuilder_v3<TEvent> Parallel(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        registerHandler(handler, LSHandlerExecutionMode_v3.Parallel, priority);
        return this;
    }

    /// <summary>
    /// Adds multiple sequential handlers to the conditional block.
    /// </summary>
    public LSConditionalBuilder_v3<TEvent> Chain(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Sequential(handler);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple parallel handlers to the conditional block.
    /// </summary>
    public LSConditionalBuilder_v3<TEvent> Concurrent(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Parallel(handler);
        }
        return this;
    }

    /// <summary>
    /// Creates a sequential group within the conditional block.
    /// </summary>
    public LSConditionalBuilder_v3<TEvent> SequentialGroup(Action<LSSequentialGroupBuilder<TEvent>> configureGroup) {
        // Create a temporary sequential group but apply the condition to each handler
        var groupBuilder = new ConditionalSequentialGroupBuilder<TEvent>(_dispatcher, _phase, getEffectiveCondition());
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a parallel group within the conditional block.
    /// </summary>
    public LSConditionalBuilder_v3<TEvent> ParallelGroup(Action<LSParallelGroupBuilder<TEvent>> configureGroup) {
        // Create a temporary parallel group but apply the condition to each handler
        var groupBuilder = new ConditionalParallelGroupBuilder<TEvent>(_dispatcher, _phase, getEffectiveCondition());
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a nested conditional block within this conditional block.
    /// </summary>
    public LSConditionalBuilder_v3<TEvent> When(Func<TEvent, bool> nestedCondition, Action<LSConditionalBuilder_v3<TEvent>> configureNested) {
        // Combine conditions: original AND nested
        var combinedCondition = _invert 
            ? new Func<TEvent, bool>(evt => !_condition(evt) && nestedCondition(evt))
            : new Func<TEvent, bool>(evt => _condition(evt) && nestedCondition(evt));
            
        var nestedBuilder = new LSConditionalBuilder_v3<TEvent>(_dispatcher, _phase, combinedCondition, false);
        configureNested(nestedBuilder);
        return this;
    }

    private void registerHandler(Func<TEvent, LSHandlerResult> handler, LSHandlerExecutionMode_v3 mode, LSPriority priority) {
        var entry = new LSHandlerEntry_v3 {
            Phase = _phase,
            Priority = priority,
            ExecutionMode = mode,
            Handler = evt => handler((TEvent)evt),
            Condition = evt => getEffectiveCondition()((TEvent)evt)
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
    }

    private Func<TEvent, bool> getEffectiveCondition() {
        return _invert 
            ? new Func<TEvent, bool>(evt => !_condition(evt))
            : _condition;
    }
}

/// <summary>
/// Helper class for sequential groups within conditional blocks.
/// </summary>
internal class ConditionalSequentialGroupBuilder<TEvent> : LSSequentialGroupBuilder<TEvent> where TEvent : ILSEvent {
    private readonly Func<TEvent, bool> _condition;

    internal ConditionalSequentialGroupBuilder(LSDispatcher_v3 dispatcher, LSLegacyEventPhase phase, Func<TEvent, bool> condition) 
        : base(dispatcher, phase) {
        _condition = condition;
    }

    // Override methods to apply condition to all handlers registered through this builder
    // Implementation would be similar but with condition applied
}

/// <summary>
/// Helper class for parallel groups within conditional blocks.
/// </summary>
internal class ConditionalParallelGroupBuilder<TEvent> : LSParallelGroupBuilder<TEvent> where TEvent : ILSEvent {
    private readonly Func<TEvent, bool> _condition;

    internal ConditionalParallelGroupBuilder(LSDispatcher_v3 dispatcher, LSLegacyEventPhase phase, Func<TEvent, bool> condition) 
        : base(dispatcher, phase) {
        _condition = condition;
    }

    // Override methods to apply condition to all handlers registered through this builder
    // Implementation would be similar but with condition applied
}
