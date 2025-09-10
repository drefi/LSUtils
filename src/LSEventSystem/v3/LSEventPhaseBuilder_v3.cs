using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Event-scoped phase builder for v3 that provides the same nested callback API.
/// </summary>
public class LSEventPhaseBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly LSEventCallbackBuilder_v3<TEvent> _builder;
    private readonly LSLegacyEventPhase _phase;

    internal LSEventPhaseBuilder_v3(LSEventCallbackBuilder_v3<TEvent> builder, LSLegacyEventPhase phase) {
        _builder = builder;
        _phase = phase;
    }

    #region Basic Registration

    /// <summary>
    /// Registers a sequential handler.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> Sequential(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Sequential, priority);
        return this;
    }

    /// <summary>
    /// Registers a parallel handler.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> Parallel(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Parallel, priority);
        return this;
    }

    /// <summary>
    /// Registers a handler (alias for sequential).
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> Handler(Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        return Sequential(handler, priority);
    }

    #endregion

    #region Group Builders

    /// <summary>
    /// Creates a sequential group builder for multiple handlers that must execute in order.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> SequentialGroup(Action<LSEventSequentialGroupBuilder_v3<TEvent>> configureGroup) {
        var groupBuilder = new LSEventSequentialGroupBuilder_v3<TEvent>(_builder, _phase);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a parallel group builder for multiple handlers that can execute independently.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> ParallelGroup(Action<LSEventParallelGroupBuilder_v3<TEvent>> configureGroup) {
        var groupBuilder = new LSEventParallelGroupBuilder_v3<TEvent>(_builder, _phase);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a generic group builder that can contain mixed handler types.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> Group(Action<LSEventPhaseBuilder_v3<TEvent>> configureGroup) {
        configureGroup(this);
        return this;
    }

    #endregion

    #region Priority Builders

    /// <summary>
    /// Creates a high priority group builder.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> HighPriority(Action<LSEventPriorityGroupBuilder_v3<TEvent>> configureGroup) {
        var groupBuilder = new LSEventPriorityGroupBuilder_v3<TEvent>(_builder, _phase, LSPriority.HIGH);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a normal priority group builder.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> NormalPriority(Action<LSEventPriorityGroupBuilder_v3<TEvent>> configureGroup) {
        var groupBuilder = new LSEventPriorityGroupBuilder_v3<TEvent>(_builder, _phase, LSPriority.NORMAL);
        configureGroup(groupBuilder);
        return this;
    }

    /// <summary>
    /// Creates a low priority group builder.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> LowPriority(Action<LSEventPriorityGroupBuilder_v3<TEvent>> configureGroup) {
        var groupBuilder = new LSEventPriorityGroupBuilder_v3<TEvent>(_builder, _phase, LSPriority.LOW);
        configureGroup(groupBuilder);
        return this;
    }

    #endregion

    #region Conditional Builders

    /// <summary>
    /// Registers handlers that execute only when the condition is true.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> When(Func<TEvent, bool> condition, Action<LSEventConditionalBuilder_v3<TEvent>> configureConditional) {
        var conditionalBuilder = new LSEventConditionalBuilder_v3<TEvent>(_builder, _phase, condition, false);
        configureConditional(conditionalBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers that execute only when the condition is false.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> Unless(Func<TEvent, bool> condition, Action<LSEventConditionalBuilder_v3<TEvent>> configureConditional) {
        var conditionalBuilder = new LSEventConditionalBuilder_v3<TEvent>(_builder, _phase, condition, true);
        configureConditional(conditionalBuilder);
        return this;
    }

    /// <summary>
    /// Registers a conditional handler directly.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> When(Func<TEvent, bool> condition, Func<TEvent, LSHandlerResult> handler, LSPriority priority = LSPriority.NORMAL) {
        _builder.RegisterEventScopedHandler(_phase, handler, LSHandlerExecutionMode_v3.Sequential, priority, condition);
        return this;
    }

    #endregion

    #region Chain Methods

    /// <summary>
    /// Registers multiple sequential handlers that execute in order.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> Chain(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Sequential(handler);
        }
        return this;
    }

    /// <summary>
    /// Registers multiple parallel handlers that execute independently.
    /// </summary>
    public LSEventPhaseBuilder_v3<TEvent> Concurrent(params Func<TEvent, LSHandlerResult>[] handlers) {
        foreach (var handler in handlers) {
            Parallel(handler);
        }
        return this;
    }

    #endregion
}
