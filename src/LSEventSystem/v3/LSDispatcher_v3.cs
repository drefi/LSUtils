using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Simple event dispatcher with nested callback builder API.
/// Focuses on clean, readable event handler registration without threading complexity.
/// "Parallel" in this context means handlers that don't depend on each other's execution order,
/// not actual thread parallelism.
/// 
/// Version 3 fixes:
/// - Preserves registration order within priority groups
/// - Properly combines global and event-scoped handlers
/// - Eliminates temporary dispatcher issues
/// </summary>
public class LSDispatcher_v3 {
    /// <summary>
    /// Gets the singleton instance of the dispatcher.
    /// </summary>
    public static LSDispatcher_v3 Singleton { get; } = new LSDispatcher_v3();

    private readonly Dictionary<Type, List<LSHandlerEntry_v3>> _handlers = new();

    /// <summary>
    /// Creates a fluent builder for registering event handlers with nested callback blocks.
    /// </summary>
    public LSEventRegister_v3<TEvent> ForEvent<TEvent>() where TEvent : ILSEvent {
        return new LSEventRegister_v3<TEvent>(this);
    }

    /// <summary>
    /// Gets handlers for a specific event type (used for combining with event-scoped handlers).
    /// Cannot be called publicly.
    /// </summary>
    internal List<LSHandlerEntry_v3> getHandlersForEvent<TEvent>() where TEvent : ILSEvent {
        var eventType = typeof(TEvent);
        return _handlers.TryGetValue(eventType, out var handlers)
            ? new List<LSHandlerEntry_v3>(handlers)
            : new List<LSHandlerEntry_v3>();
    }
    internal List<LSHandlerEntry_v3> getHandlersForEvent(System.Type eventType) {
        return _handlers.TryGetValue(eventType, out var handlers)
            ? new List<LSHandlerEntry_v3>(handlers)
            : new List<LSHandlerEntry_v3>();
    }
    /// <summary>
    /// Registers a handler entry.
    /// Cannot be called publicly.
    /// </summary>
    internal void registerHandler<TEvent>(LSHandlerEntry_v3 entry) where TEvent : ILSEvent {
        entry.IsEventScoped = false; // Global handlers

        var eventType = typeof(TEvent);
        if (!_handlers.ContainsKey(eventType)) {
            _handlers[eventType] = new List<LSHandlerEntry_v3>();
        }
        _handlers[eventType].Add(entry);
    }
    /// <summary>
    /// Processes an event with a specific list of handlers (used for combined global + event-scoped processing).
    /// Cannot be called publicly.
    /// </summary>
    internal bool processEventWithHandlers(ILSEvent @event, List<LSHandlerEntry_v3> handlers) {
        // Process core phases: VALIDATE → PREPARE → EXECUTE
        var corePhases = new[] { LSLegacyEventPhase.VALIDATE, LSLegacyEventPhase.PREPARE, LSLegacyEventPhase.EXECUTE };

        foreach (var phase in corePhases) {
            // If the event is our base event type, we can set the properties
            if (@event is LSBaseEvent_v3 baseEvent) {
                baseEvent.CurrentPhase = phase;
            }

            if (!processPhase(@event, phase, handlers)) {
                return false;
            }

            // If the event is our base event type, we can set the properties
            if (@event is LSBaseEvent_v3 baseEvent2) {
                baseEvent2.CompletedPhases |= phase;
            }

            // Check for cancellation after each core phase
            if (@event.IsCancelled) {
                // Process cancellation flow: CANCEL → COMPLETE
                processPhaseAndMarkCompleted(@event, LSLegacyEventPhase.CANCEL, handlers);
                processPhaseAndMarkCompleted(@event, LSLegacyEventPhase.COMPLETE, handlers);
                return false; // Cancelled events are not successful
            }
        }

        // Determine next phase based on event state
        if (@event is LSBaseEvent_v3 baseEvent3 && baseEvent3.HasFailures) {
            // Failure flow: FAILURE → COMPLETE
            processPhaseAndMarkCompleted(@event, LSLegacyEventPhase.FAILURE, handlers);
        } else {
            // Success flow: SUCCESS → COMPLETE
            processPhaseAndMarkCompleted(@event, LSLegacyEventPhase.SUCCESS, handlers);
        }

        // Always process COMPLETE phase
        processPhaseAndMarkCompleted(@event, LSLegacyEventPhase.COMPLETE, handlers);

        // If the event is our base event type, we can set the properties
        if (@event is LSBaseEvent_v3 finalBaseEvent) {
            finalBaseEvent.IsCompleted = true;
        }
        return true;
    }

    private void processPhaseAndMarkCompleted(ILSEvent @event, LSLegacyEventPhase phase, List<LSHandlerEntry_v3> handlers) {
        if (@event is LSBaseEvent_v3 baseEvent) {
            baseEvent.CurrentPhase = phase;
        }

        processPhase(@event, phase, handlers);

        if (@event is LSBaseEvent_v3 baseEvent2) {
            baseEvent2.CompletedPhases |= phase;
        }
    }

    private bool processPhase(ILSEvent @event, LSLegacyEventPhase phase, List<LSHandlerEntry_v3> handlers) {
        // Get handlers for this phase, ordered by priority FIRST, then by registration order
        // This preserves the intended execution flow: handlerA -> handlerB & handlerC -> handlerD
        var phaseHandlers = handlers
            .Where(h => h.Phase == phase)
            .OrderBy(h => (int)h.Priority)           // Priority first (HIGH=0, NORMAL=1, LOW=2)
            .ToList();

        if (!phaseHandlers.Any()) {
            return true;
        }

        // Execute handlers in the order they were registered (within priority groups)
        // This preserves the intended execution flow while still respecting priority
        foreach (var handler in phaseHandlers) {
            if (!executeHandler(@event, handler)) {
                return false;
            }
            // Note: Sequential vs Parallel is now just metadata - 
            // actual parallel execution would require async implementation
        }

        return true;
    }

    private bool executeHandler(ILSEvent @event, LSHandlerEntry_v3 handlerEntry) {
        try {
            // Check condition if present
            if (handlerEntry.Condition != null && !handlerEntry.Condition(@event)) {
                return true; // Condition not met, skip handler but continue processing
            }

            var result = handlerEntry.Handler(@event);
            handlerEntry.Executions++;

            switch (result) {
                case LSHandlerResult.CONTINUE:
                    return true;
                case LSHandlerResult.SKIP_REMAINING:
                    return true; // Skip remaining handlers in this phase but continue to next phase
                case LSHandlerResult.CANCEL:
                    // If the event is our base event type, we can set the properties
                    if (@event is LSBaseEvent_v3 baseEvent) {
                        baseEvent.IsCancelled = true;
                    }
                    return true; // Continue processing to allow CANCEL phase handlers
                case LSHandlerResult.FAILURE:
                    // If the event is our base event type, we can set the properties
                    if (@event is LSBaseEvent_v3 baseEvent2) {
                        baseEvent2.HasFailures = true;
                    }
                    return true; // Continue processing to allow FAILURE phase handlers
                case LSHandlerResult.RETRY:
                    // For now, treat as continue. Future versions could implement retry logic
                    return true;
                case LSHandlerResult.WAITING:
                    // For now, treat as continue. Future versions could implement async waiting
                    return true;
                default:
                    return false;
            }
        } catch (Exception) {
            // If the event is our base event type, we can set the properties
            if (@event is LSBaseEvent_v3 baseEvent) {
                baseEvent.HasFailures = true;
            }
            return false;
        }
    }
}
