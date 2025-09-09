using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Event-scoped callback builder for v3 that provides the same nested callback API as the global dispatcher
/// but registers handlers that only execute for the specific event instance and are automatically cleaned up.
/// 
/// FIXED: Now properly combines global and event-scoped handlers without temporary dispatcher issues.
/// </summary>
/// <typeparam name="TEvent">The event type this builder is configured for.</typeparam>
public class LSEventCallbackBuilder_v3<TEvent> where TEvent : ILSEvent {
    private readonly TEvent _event;
    private readonly LSDispatcher_v3 _dispatcher;
    private readonly List<LSEventScopedHandler_v3> _eventScopedHandlers = new();
    private bool _isFinalized = false;
    private int _eventScopedRegistrationCounter = 0;

    /// <summary>
    /// Initializes a new v3 callback builder for the specified event and dispatcher.
    /// </summary>
    /// <param name="event">The event instance this builder is bound to.</param>
    /// <param name="dispatcher">The v3 dispatcher to process the event with.</param>
    internal LSEventCallbackBuilder_v3(TEvent @event, LSDispatcher_v3 dispatcher) {
        _event = @event;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Gets the target event instance this builder is bound to.
    /// </summary>
    public TEvent TargetEvent => _event;

    /// <summary>
    /// Gets whether this builder has been finalized and can no longer accept new handlers.
    /// </summary>
    public bool IsFinalized => _isFinalized;

    #region Phase-Based Registration with Nested Callbacks

    /// <summary>
    /// Registers handlers for the VALIDATE phase with nested callback support.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnValidate(Action<LSEventPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSEventPhaseBuilder_v3<TEvent>(this, LSEventPhase.VALIDATE);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the PREPARE phase with nested callback support.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnPrepare(Action<LSEventPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSEventPhaseBuilder_v3<TEvent>(this, LSEventPhase.PREPARE);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the EXECUTE phase with nested callback support.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnExecute(Action<LSEventPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSEventPhaseBuilder_v3<TEvent>(this, LSEventPhase.EXECUTE);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the SUCCESS phase with nested callback support.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnSuccess(Action<LSEventPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSEventPhaseBuilder_v3<TEvent>(this, LSEventPhase.SUCCESS);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the FAILURE phase with nested callback support.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnFailure(Action<LSEventPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSEventPhaseBuilder_v3<TEvent>(this, LSEventPhase.FAILURE);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the CANCEL phase with nested callback support.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnCancel(Action<LSEventPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSEventPhaseBuilder_v3<TEvent>(this, LSEventPhase.CANCEL);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the COMPLETE phase with nested callback support.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnComplete(Action<LSEventPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSEventPhaseBuilder_v3<TEvent>(this, LSEventPhase.COMPLETE);
        configurePhase(phaseBuilder);
        return this;
    }

    #endregion

    #region Simple Direct Registration

    /// <summary>
    /// Registers a sequential handler for a specific phase.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnSequential(LSEventPhase phase, Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        RegisterEventScopedHandler(phase, handler, LSHandlerExecutionMode_v3.Sequential, priority);
        return this;
    }

    /// <summary>
    /// Registers a parallel handler for a specific phase.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnParallel(LSEventPhase phase, Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        RegisterEventScopedHandler(phase, handler, LSHandlerExecutionMode_v3.Parallel, priority);
        return this;
    }

    #endregion

    #region Dispatching

    /// <summary>
    /// Dispatches the event through the dispatcher, combining both global and event-scoped handlers.
    /// 
    /// FIXED: Now properly combines global and event-scoped handlers in registration order.
    /// </summary>
    /// <returns>
    /// True if the event completed successfully through all phases, 
    /// false if it was cancelled, had critical failures, or is waiting for async operations.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if the event has already been dispatched.</exception>
    public bool Dispatch() {
        if (_isFinalized) {
            throw new InvalidOperationException("Event has already been dispatched");
        }
        _isFinalized = true;

        // FIXED APPROACH: Get global handlers and combine with event-scoped handlers
        // Get global handlers from the main dispatcher
        var globalHandlers = _dispatcher.getHandlersForEvent<TEvent>();

        // Create combined handler list maintaining registration order
        var eventScopeHandlers = new List<LSHandlerEntry_v3>();

        // Add global handlers (they keep their original registration order)
        eventScopeHandlers.AddRange(globalHandlers);

        // Convert and add event-scoped handlers with proper registration order
        foreach (var eventScopedHandler in _eventScopedHandlers) {
            eventScopeHandlers.Add(new LSHandlerEntry_v3 {
                Phase = eventScopedHandler.Phase,
                Priority = eventScopedHandler.Priority,
                ExecutionMode = eventScopedHandler.ExecutionMode,
                Handler = eventScopedHandler.Handler,
                Condition = eventScopedHandler.Condition,
                RegistrationOrder = eventScopedHandler.RegistrationOrder,
                IsEventScoped = true
            });
        }

        // Process with combined handlers using the new method
        return _dispatcher.processEventWithHandlers(_event, eventScopeHandlers);
    }

    #endregion

    #region Simple Callback Methods (Convenience)

    /// <summary>
    /// Registers a simple success callback that executes when the event completes successfully.
    /// This is a convenience method that registers a sequential handler in the SUCCESS phase.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnSuccessAction(Action<TEvent> callback) {
        return OnSuccess(success => success
            .Sequential(evt => {
                callback(evt);
                return LSHandlerResult.CONTINUE;
            })
        );
    }

    /// <summary>
    /// Registers a simple failure callback that executes when the event has failures.
    /// This is a convenience method that registers a sequential handler in the FAILURE phase.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnFailureAction(Action<TEvent> callback) {
        return OnFailure(failure => failure
            .Sequential(evt => {
                callback(evt);
                return LSHandlerResult.CONTINUE;
            })
        );
    }

    /// <summary>
    /// Registers a simple cancel callback that executes when the event is cancelled.
    /// This is a convenience method that registers a sequential handler in the CANCEL phase.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnCancelAction(Action<TEvent> callback) {
        return OnCancel(cancel => cancel
            .Sequential(evt => {
                callback(evt);
                return LSHandlerResult.CONTINUE;
            })
        );
    }

    /// <summary>
    /// Registers a simple completion callback that executes when the event completes (success or failure).
    /// This is a convenience method that registers a sequential handler in the COMPLETE phase.
    /// </summary>
    public LSEventCallbackBuilder_v3<TEvent> OnCompleteAction(Action<TEvent> callback) {
        return OnComplete(complete => complete
            .Sequential(evt => {
                callback(evt);
                return LSHandlerResult.CONTINUE;
            })
        );
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Registers an event-scoped handler that will be combined with global handlers during dispatch.
    /// </summary>
    internal void RegisterEventScopedHandler(LSEventPhase phase, Func<TEvent, LSHandlerResult> handler, LSHandlerExecutionMode_v3 mode, LSESPriority priority, Func<TEvent, bool>? condition = null) {
        if (_isFinalized) {
            throw new InvalidOperationException("Cannot add handlers to a finalized builder");
        }

        _eventScopedHandlers.Add(new LSEventScopedHandler_v3 {
            Phase = phase,
            Priority = priority,
            ExecutionMode = mode,
            Handler = evt => handler((TEvent)evt),
            Condition = condition != null ? evt => condition((TEvent)evt) : null,
            RegistrationOrder = ++_eventScopedRegistrationCounter
        });
    }

    #endregion
}
