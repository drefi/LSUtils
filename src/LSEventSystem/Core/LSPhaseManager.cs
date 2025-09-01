using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem.Core;

/// <summary>
/// Internal utility class for managing event phase execution flow and lifecycle.
/// Separates phase management concerns from the main dispatcher logic.
/// </summary>
internal static class LSPhaseManager {
    /// <summary>
    /// Gets the standard phase execution order for events.
    /// </summary>
    /// <returns>Array of phases in execution order.</returns>
    public static LSEventPhase[] GetPhaseExecutionOrder() {
        return new[] {
            LSEventPhase.VALIDATE,
            LSEventPhase.PREPARE,
            LSEventPhase.EXECUTE,
            LSEventPhase.SUCCESS,
            LSEventPhase.FAILURE,
            LSEventPhase.CANCEL,
            LSEventPhase.COMPLETE
        };
    }

    /// <summary>
    /// Determines if a phase should be skipped based on event state.
    /// </summary>
    /// <typeparam name="TEvent">The event type being processed.</typeparam>
    /// <param name="event">The event instance.</param>
    /// <param name="phase">The phase to check.</param>
    /// <returns>True if the phase should be skipped.</returns>
    public static bool ShouldSkipPhase<TEvent>(TEvent @event, LSEventPhase phase) where TEvent : ILSEvent {
        return phase switch {
            LSEventPhase.SUCCESS => @event.IsCancelled || @event.HasFailures,
            LSEventPhase.FAILURE => @event.IsCancelled || !@event.HasFailures,
            LSEventPhase.CANCEL => !@event.IsCancelled,
            _ => false
        };
    }

    /// <summary>
    /// Determines the appropriate resumption phase based on event state.
    /// </summary>
    /// <typeparam name="TEvent">The event type being processed.</typeparam>
    /// <param name="event">The event instance.</param>
    /// <returns>The phase to resume from.</returns>
    public static LSEventPhase DetermineResumptionPhase<TEvent>(TEvent @event) where TEvent : ILSEvent {
        var mutableEvent = (ILSMutableEvent)@event;
        
        // If event was cancelled during async operation, go to CANCEL phase
        if (@event.IsCancelled && mutableEvent.CurrentPhase != LSEventPhase.CANCEL && mutableEvent.CurrentPhase != LSEventPhase.COMPLETE) {
            return LSEventPhase.CANCEL;
        }

        // If event has failures during async operation, go to FAILURE phase
        if (@event.HasFailures && mutableEvent.CurrentPhase != LSEventPhase.FAILURE && 
            mutableEvent.CurrentPhase != LSEventPhase.CANCEL && mutableEvent.CurrentPhase != LSEventPhase.COMPLETE) {
            return LSEventPhase.FAILURE;
        }

        // Continue from the next phase in the normal sequence
        var phases = GetPhaseExecutionOrder();
        var currentIndex = System.Array.IndexOf(phases, mutableEvent.CurrentPhase);
        
        if (currentIndex >= 0 && currentIndex < phases.Length - 1) {
            return phases[currentIndex + 1];
        }

        // Default to COMPLETE if no clear next phase
        return LSEventPhase.COMPLETE;
    }

    /// <summary>
    /// Processes the result of a phase execution and updates event state accordingly.
    /// </summary>
    /// <typeparam name="TEvent">The event type being processed.</typeparam>
    /// <param name="event">The event instance.</param>
    /// <param name="phase">The phase that was executed.</param>
    /// <param name="result">The result of the phase execution.</param>
    /// <returns>True if processing should continue, false if it should stop.</returns>
    public static bool ProcessPhaseResult<TEvent>(TEvent @event, LSEventPhase phase, LSPhaseExecutionResult result) where TEvent : ILSEvent {
        var mutableEvent = (ILSMutableEvent)@event;

        if (result.ShouldWait) {
            mutableEvent.IsWaiting = true;
            return false; // Stop processing until resumed
        }
        
        if (result.ShouldCancel) {
            mutableEvent.IsCancelled = true;
            if (!string.IsNullOrEmpty(result.ErrorMessage) && @event is LSBaseEvent baseEvent) {
                baseEvent.SetErrorMessage(result.ErrorMessage);
            }
            return true; // Continue to CANCEL/COMPLETE phases
        }
        
        if (result.HasFailures) {
            mutableEvent.HasFailures = true;
            if (!string.IsNullOrEmpty(result.ErrorMessage) && @event is LSBaseEvent baseEvent) {
                baseEvent.SetErrorMessage(result.ErrorMessage);
            }
            return true; // Continue to FAILURE/COMPLETE phases
        }

        // Mark phase as completed
        mutableEvent.CompletedPhases |= phase;
        return result.Success;
    }
}
