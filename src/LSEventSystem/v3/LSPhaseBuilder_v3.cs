using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Nested callback builder for configuring handlers within a specific phase.
/// Provides BehaviourTreeBuilder-style fluent API with nesting support.
/// </summary>
public class LSPhaseBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly LSDispatcher_v3 _dispatcher;
    private readonly LSEventPhase _phase;

    internal LSPhaseBuilder_v3(LSDispatcher_v3 dispatcher, LSEventPhase phase) {
        _dispatcher = dispatcher;
        _phase = phase;
    }

    #region Basic Registration

    /// <summary>
    /// Registers a sequential handler.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> Sequential(Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        registerHandler(handler, LSHandlerExecutionMode_v3.Sequential, priority);
        return this;
    }

    /// <summary>
    /// Registers a parallel handler.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> Parallel(Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        registerHandler(handler, LSHandlerExecutionMode_v3.Parallel, priority);
        return this;
    }

    /// <summary>
    /// Registers a handler (alias for sequential).
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> Handler(Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        return Sequential(handler, priority);
    }

    #endregion

    #region Group Builders

    /// <summary>
    /// Creates a sequential group builder for multiple handlers that must execute in order.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> SequentialGroup(Action<LSSequentialGroupBuilder<TEvent>> configureGroup) {
        var groupBuilder = new LSSequentialGroupBuilder<TEvent>(_dispatcher, _phase);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a parallel group builder for multiple handlers that can execute independently.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> ParallelGroup(Action<LSParallelGroupBuilder<TEvent>> configureGroup) {
        var groupBuilder = new LSParallelGroupBuilder<TEvent>(_dispatcher, _phase);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a generic group builder that can contain mixed handler types.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> Group(Action<LSPhaseBuilder_v3<TEvent>> configureGroup) {
        configureGroup(this);
        return this;
    }

    #endregion

    #region Priority Builders

    /// <summary>
    /// Creates a high priority group builder.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> HighPriority(Action<LSPriorityGroupBuilder<TEvent>> configureGroup) {
        var groupBuilder = new LSPriorityGroupBuilder<TEvent>(_dispatcher, _phase, LSESPriority.HIGH);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a normal priority group builder.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> NormalPriority(Action<LSPriorityGroupBuilder<TEvent>> configureGroup) {
        var groupBuilder = new LSPriorityGroupBuilder<TEvent>(_dispatcher, _phase, LSESPriority.NORMAL);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a low priority group builder.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> LowPriority(Action<LSPriorityGroupBuilder<TEvent>> configureGroup) {
        var groupBuilder = new LSPriorityGroupBuilder<TEvent>(_dispatcher, _phase, LSESPriority.LOW);
        configureGroup(groupBuilder);
        return this;
    }

    #endregion

    #region Conditional Builders

    /// <summary>
    /// Registers handlers that execute only when the condition is true.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> When(Func<TEvent, bool> condition, Action<LSConditionalBuilder_v3<TEvent>> configureConditional) {
        var conditionalBuilder = new LSConditionalBuilder_v3<TEvent>(_dispatcher, _phase, condition, false);
        configureConditional(conditionalBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers that execute only when the condition is false.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> Unless(Func<TEvent, bool> condition, Action<LSConditionalBuilder_v3<TEvent>> configureConditional) {
        var conditionalBuilder = new LSConditionalBuilder_v3<TEvent>(_dispatcher, _phase, condition, true);
        configureConditional(conditionalBuilder);
        return this;
    }

    /// <summary>
    /// Registers a conditional handler directly.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> When(Func<TEvent, bool> condition, Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        registerConditionalHandler(handler, LSHandlerExecutionMode_v3.Sequential, priority, condition);
        return this;
    }

    #endregion

    #region Chain Methods

    /// <summary>
    /// Registers multiple sequential handlers that execute in order.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> Chain(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Sequential(handler);
        }
        return this;
    }

    /// <summary>
    /// Registers multiple parallel handlers that execute independently.
    /// </summary>
    public LSPhaseBuilder_v3<TEvent> Concurrent(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Parallel(handler);
        }
        return this;
    }

    #endregion

    private void registerHandler(Func<TEvent, LSHandlerResult> handler, LSHandlerExecutionMode_v3 mode, LSESPriority priority) {
        var entry = new LSHandlerEntry_v3 {
            Phase = _phase,
            Priority = priority,
            ExecutionMode = mode,
            Handler = evt => handler((TEvent)evt)
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
    }

    private void registerConditionalHandler(Func<TEvent, LSHandlerResult> handler, LSHandlerExecutionMode_v3 mode, LSESPriority priority, Func<TEvent, bool> condition) {
        var entry = new LSHandlerEntry_v3 {
            Phase = _phase,
            Priority = priority,
            ExecutionMode = mode,
            Handler = evt => handler((TEvent)evt),
            Condition = evt => condition((TEvent)evt)
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
    }
}
