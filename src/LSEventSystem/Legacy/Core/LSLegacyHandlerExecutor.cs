using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Internal utility class for managing handler execution and result processing.
/// Separates handler execution logic from the main dispatcher.
/// </summary>
internal static class LSLegacyHandlerExecutor {
    /// <summary>
    /// Executes a single handler and processes its result.
    /// </summary>
    /// <typeparam name="TEvent">The event type being processed.</typeparam>
    /// <param name="event">The event instance.</param>
    /// <param name="handler">The handler to execute.</param>
    /// <param name="phase">The current phase.</param>
    /// <param name="startTime">The phase start time for context.</param>
    /// <param name="errors">List to collect any errors.</param>
    /// <param name="logAction">Action to log events during execution.</param>
    /// <returns>The execution result.</returns>
    public static LSLegacyPhaseExecutionResult ExecuteHandler<TEvent>(
        TEvent @event, 
        LSLegacyHandlerRegistration handler, 
        LSLegacyEventPhase phase, 
        DateTime startTime, 
        List<string> errors,
        Action<TEvent, string, string, object?> logAction) where TEvent : ILSEvent {
        
        try {
            var elapsed = DateTime.UtcNow - startTime;
            var context = new LSLegacyPhaseContext(phase, handler.Priority, elapsed, 0, errors);

            logAction(@event, "ExecuteHandler", $"Executing handler {handler.Id} in phase {phase}", null);
            
            var result = handler.Handler(@event, context);
            handler.ExecutionCount++;

            logAction(@event, "ExecuteHandler", $"Handler {handler.Id} in phase {phase} returned {result}", null);

            return MapHandlerResult(@event, handler, result);
        }
        catch (LSException ex) {
            return HandleLSException(@event, handler, phase, ex, errors, logAction);
        }
        catch (Exception ex) {
            return HandleUnexpectedException(@event, handler, phase, ex, errors, logAction);
        }
    }

    /// <summary>
    /// Maps a handler result to a phase execution result.
    /// </summary>
    private static LSLegacyPhaseExecutionResult MapHandlerResult<TEvent>(
        TEvent @event, 
        LSLegacyHandlerRegistration handler, 
        LSHandlerResult result) where TEvent : ILSEvent {
        
        return result switch {
            LSHandlerResult.CONTINUE => LSLegacyPhaseExecutionResult.Successful(),
            LSHandlerResult.SKIP_REMAINING => LSLegacyPhaseExecutionResult.Successful(),
            LSHandlerResult.CANCEL => LSLegacyPhaseExecutionResult.Cancelled(
                GetErrorMessage(@event) ?? $"Handler {handler.Id} cancelled event"),
            LSHandlerResult.FAILURE => LSLegacyPhaseExecutionResult.Failed(
                GetErrorMessage(@event) ?? $"Handler {handler.Id} reported failure"),
            LSHandlerResult.WAITING => LSLegacyPhaseExecutionResult.Waiting(),
            LSHandlerResult.RETRY => HandleRetryRequest<TEvent>(handler),
            _ => LSLegacyPhaseExecutionResult.Successful()
        };
    }

    /// <summary>
    /// Handles retry logic for a handler.
    /// </summary>
    private static LSLegacyPhaseExecutionResult HandleRetryRequest<TEvent>(LSLegacyHandlerRegistration handler) where TEvent : ILSEvent {
        if (handler.ExecutionCount < 3) {
            return LSLegacyPhaseExecutionResult.Successful(); // Allow retry
        }
        
        return LSLegacyPhaseExecutionResult.Failed($"Handler {handler.Id} exceeded retry limit");
    }

    /// <summary>
    /// Handles LSException during handler execution.
    /// </summary>
    private static LSLegacyPhaseExecutionResult HandleLSException<TEvent>(
        TEvent @event, 
        LSLegacyHandlerRegistration handler, 
        LSLegacyEventPhase phase, 
        LSException ex, 
        List<string> errors,
        Action<TEvent, string, string, object?> logAction) where TEvent : ILSEvent {
        
        var errorMsg = $"Handler {handler.Id} in phase {phase} failed with LSException: {ex.Message}";
        errors.Add(errorMsg);
        logAction(@event, "HandleLSException", errorMsg, null);
        
        // Critical validation failures should stop processing
        if (phase == LSLegacyEventPhase.VALIDATE && handler.Priority == LSPriority.CRITICAL) {
            return LSLegacyPhaseExecutionResult.Cancelled("Critical validation failure");
        }
        
        return LSLegacyPhaseExecutionResult.Successful(); // Continue with other handlers
    }

    /// <summary>
    /// Handles unexpected exceptions during handler execution.
    /// </summary>
    private static LSLegacyPhaseExecutionResult HandleUnexpectedException<TEvent>(
        TEvent @event, 
        LSLegacyHandlerRegistration handler, 
        LSLegacyEventPhase phase, 
        Exception ex, 
        List<string> errors,
        Action<TEvent, string, string, object?> logAction) where TEvent : ILSEvent {
        
        var errorMsg = $"Handler {handler.Id} in phase {phase} failed with unexpected exception: {ex.Message}";
        errors.Add(errorMsg);
        logAction(@event, "HandleUnexpectedException", errorMsg, null);
        
        // Critical validation failures should stop processing
        if (phase == LSLegacyEventPhase.VALIDATE && handler.Priority == LSPriority.CRITICAL) {
            return LSLegacyPhaseExecutionResult.Cancelled("Critical validation failure");
        }
        
        // Re-throw exceptions that aren't handled by the event system
        // This allows tests and client code to catch exceptions as expected
        throw new InvalidOperationException($"Handler {handler.Id} failed unexpectedly", ex);
    }

    /// <summary>
    /// Gets the error message from an event if it's a LSBaseEvent.
    /// </summary>
    private static string? GetErrorMessage<TEvent>(TEvent @event) where TEvent : ILSEvent {
        return @event is LSLegacyBaseEvent baseEvent && !string.IsNullOrEmpty(baseEvent.ErrorMessage) 
            ? baseEvent.ErrorMessage 
            : null;
    }
}
