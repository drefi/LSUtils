using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Internal utility class for managing handler filtering and selection logic.
/// Separates handler filtering concerns from the main dispatcher logic.
/// </summary>
internal static class LSLegacyHandlerFilter {
    /// <summary>
    /// Filters handlers for a specific phase based on execution limits, instance matching, and conditions.
    /// </summary>
    /// <typeparam name="TEvent">The event type being processed.</typeparam>
    /// <param name="allHandlers">All registered handlers for the event type.</param>
    /// <param name="phase">The phase to filter handlers for.</param>
    /// <param name="event">The event instance being processed.</param>
    /// <returns>Filtered list of handlers ready for execution.</returns>
    public static List<LSLegacyHandlerRegistration> FilterHandlersForPhase<TEvent>(
        List<LSLegacyHandlerRegistration> allHandlers, 
        LSLegacyEventPhase phase, 
        TEvent @event) where TEvent : ILSEvent {
        
        return allHandlers
            .Where(h => h.Phase == phase)
            .Where(h => h.MaxExecutions == -1 || h.ExecutionCount < h.MaxExecutions)
            .Where(h => matchesInstance(h, @event))
            .Where(h => h.Condition == null || h.Condition(@event))
            .OrderBy(h => h.Priority)
            .ToList();
    }

    /// <summary>
    /// Checks if a handler's instance restriction matches the current event.
    /// </summary>
    /// <typeparam name="TEvent">The type of event being processed.</typeparam>
    /// <param name="handler">The handler registration to check.</param>
    /// <param name="event">The event being processed.</param>
    /// <returns>True if the handler should execute for this event.</returns>
    private static bool matchesInstance<TEvent>(LSLegacyHandlerRegistration handler, TEvent @event) where TEvent : ILSEvent {
        // Check for event-scoped handlers (batched handlers with TargetEvent)
        if (handler.TargetEvent != null) {
            return ReferenceEquals(handler.TargetEvent, @event);
        }
        
        // Check for instance-specific handlers
        if (handler.Instance == null) {
            return true;
        }

        if (@event is LSLegacyBaseEvent baseEvent) {
            var instanceProperty = baseEvent.GetType().GetProperty("Instance");
            if (instanceProperty != null) {
                var eventInstance = instanceProperty.GetValue(baseEvent);
                return ReferenceEquals(handler.Instance, eventInstance);
            }
        }

        return false;
    }
}
