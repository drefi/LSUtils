using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Internal structure representing a batched handler within an event-scoped callback batch.
/// This allows multiple handlers to be registered as a single unit for better performance.
/// </summary>
internal struct LSBatchedHandler<TEvent> where TEvent : ILSEvent {
    /// <summary>
    /// The phase in which this handler should execute.
    /// </summary>
    public LSEventPhase Phase { get; init; }
    
    /// <summary>
    /// The execution priority within the phase.
    /// </summary>
    public LSPhasePriority Priority { get; init; }
    
    /// <summary>
    /// The handler function to execute.
    /// </summary>
    public LSPhaseHandler<TEvent> Handler { get; init; }
    
    /// <summary>
    /// Optional condition that must be met for this handler to execute.
    /// </summary>
    public LSEventCondition<TEvent>? Condition { get; init; }
}

/// <summary>
/// Internal batch collector that accumulates handlers before registering them as a single unit.
/// This provides better performance than individual handler registrations while maintaining
/// the same fluent API surface.
/// </summary>
/// <typeparam name="TEvent">The event type this batch is configured for.</typeparam>
internal class LSEventCallbackBatch<TEvent> where TEvent : ILSEvent {
    private readonly List<LSBatchedHandler<TEvent>> _handlers = new();
    private readonly TEvent _targetEvent;
    
    /// <summary>
    /// Initializes a new batch collector for the specified event instance.
    /// </summary>
    /// <param name="targetEvent">The event instance this batch is bound to.</param>
    public LSEventCallbackBatch(TEvent targetEvent) {
        _targetEvent = targetEvent;
    }
    
    /// <summary>
    /// Gets the target event instance this batch is bound to.
    /// </summary>
    public TEvent TargetEvent => _targetEvent;
    
    /// <summary>
    /// Gets the collected handlers in this batch.
    /// </summary>
    public IReadOnlyList<LSBatchedHandler<TEvent>> Handlers => _handlers.AsReadOnly();
    
    /// <summary>
    /// Adds a handler to the batch for the VALIDATE phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <param name="condition">Optional condition for execution.</param>
    /// <returns>This batch for fluent chaining.</returns>
    public LSEventCallbackBatch<TEvent> OnValidation(
        LSPhaseHandler<TEvent> handler,
        LSPhasePriority priority = LSPhasePriority.NORMAL,
        LSEventCondition<TEvent>? condition = null) {
        
        _handlers.Add(new LSBatchedHandler<TEvent> {
            Phase = LSEventPhase.VALIDATE,
            Priority = priority,
            Handler = handler,
            Condition = condition
        });
        return this;
    }
    
    /// <summary>
    /// Adds a handler to the batch for the PREPARE phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <param name="condition">Optional condition for execution.</param>
    /// <returns>This batch for fluent chaining.</returns>
    public LSEventCallbackBatch<TEvent> OnPrepare(
        LSPhaseHandler<TEvent> handler,
        LSPhasePriority priority = LSPhasePriority.NORMAL,
        LSEventCondition<TEvent>? condition = null) {
        
        _handlers.Add(new LSBatchedHandler<TEvent> {
            Phase = LSEventPhase.PREPARE,
            Priority = priority,
            Handler = handler,
            Condition = condition
        });
        return this;
    }
    
    /// <summary>
    /// Adds a handler to the batch for the EXECUTE phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <param name="condition">Optional condition for execution.</param>
    /// <returns>This batch for fluent chaining.</returns>
    public LSEventCallbackBatch<TEvent> OnExecution(
        LSPhaseHandler<TEvent> handler,
        LSPhasePriority priority = LSPhasePriority.NORMAL,
        LSEventCondition<TEvent>? condition = null) {
        
        _handlers.Add(new LSBatchedHandler<TEvent> {
            Phase = LSEventPhase.EXECUTE,
            Priority = priority,
            Handler = handler,
            Condition = condition
        });
        return this;
    }
    
    /// <summary>
    /// Adds a handler to the batch for the SUCCESS phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <param name="condition">Optional condition for execution.</param>
    /// <returns>This batch for fluent chaining.</returns>
    public LSEventCallbackBatch<TEvent> OnSuccess(
        LSPhaseHandler<TEvent> handler,
        LSPhasePriority priority = LSPhasePriority.NORMAL,
        LSEventCondition<TEvent>? condition = null) {
        
        _handlers.Add(new LSBatchedHandler<TEvent> {
            Phase = LSEventPhase.SUCCESS,
            Priority = priority,
            Handler = handler,
            Condition = condition
        });
        return this;
    }
    
    /// <summary>
    /// Adds a handler to the batch for the COMPLETE phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <param name="condition">Optional condition for execution.</param>
    /// <returns>This batch for fluent chaining.</returns>
    public LSEventCallbackBatch<TEvent> OnComplete(
        LSPhaseHandler<TEvent> handler,
        LSPhasePriority priority = LSPhasePriority.NORMAL,
        LSEventCondition<TEvent>? condition = null) {
        
        _handlers.Add(new LSBatchedHandler<TEvent> {
            Phase = LSEventPhase.COMPLETE,
            Priority = priority,
            Handler = handler,
            Condition = condition
        });
        return this;
    }
    
    /// <summary>
    /// Adds a handler with critical priority to the batch for the VALIDATE phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="condition">Optional condition for execution.</param>
    /// <returns>This batch for fluent chaining.</returns>
    public LSEventCallbackBatch<TEvent> OnCriticalValidation(
        LSPhaseHandler<TEvent> handler,
        LSEventCondition<TEvent>? condition = null) {
        
        return OnValidation(handler, LSPhasePriority.CRITICAL, condition);
    }
    
    /// <summary>
    /// Adds a handler with high priority to the batch for the EXECUTE phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="condition">Optional condition for execution.</param>
    /// <returns>This batch for fluent chaining.</returns>
    public LSEventCallbackBatch<TEvent> OnHighPriorityExecution(
        LSPhaseHandler<TEvent> handler,
        LSEventCondition<TEvent>? condition = null) {
        
        return OnExecution(handler, LSPhasePriority.HIGH, condition);
    }
    
    /// <summary>
    /// Adds a handler to the batch for the CANCEL phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <param name="condition">Optional condition for execution.</param>
    /// <returns>This batch for fluent chaining.</returns>
    public LSEventCallbackBatch<TEvent> OnCancel(
        LSPhaseHandler<TEvent> handler,
        LSPhasePriority priority = LSPhasePriority.NORMAL,
        LSEventCondition<TEvent>? condition = null) {
        
        _handlers.Add(new LSBatchedHandler<TEvent> {
            Phase = LSEventPhase.CANCEL,
            Priority = priority,
            Handler = handler,
            Condition = condition
        });
        return this;
    }
    
    /// <summary>
    /// Gets the number of handlers currently in this batch.
    /// </summary>
    public int HandlerCount => _handlers.Count;
    
    /// <summary>
    /// Checks if this batch has any handlers for the specified phase.
    /// </summary>
    /// <param name="phase">The phase to check.</param>
    /// <returns>True if there are handlers for the phase, false otherwise.</returns>
    public bool HasHandlersForPhase(LSEventPhase phase) {
        return _handlers.Any(h => h.Phase == phase);
    }
    
    /// <summary>
    /// Gets all handlers for a specific phase, sorted by priority.
    /// </summary>
    /// <param name="phase">The phase to get handlers for.</param>
    /// <returns>Handlers for the phase, sorted by priority.</returns>
    public IEnumerable<LSBatchedHandler<TEvent>> GetHandlersForPhase(LSEventPhase phase) {
        return _handlers
            .Where(h => h.Phase == phase)
            .OrderBy(h => h.Priority);
    }
}
