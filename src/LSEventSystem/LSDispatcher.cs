using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Central event dispatcher that coordinates the execution of event handlers across different phases.
/// This is the main component of the event system and provides predictable, organized event processing.
/// 
/// The dispatcher processes events through phases: VALIDATE → PREPARE → EXECUTE → SUCCESS/FAILURE/CANCEL → COMPLETE
/// Each phase serves a specific purpose and handlers are executed in priority order within each phase.
/// 
/// Key Features:
/// - Thread-safe handler registration and execution
/// - Priority-based execution within phases
/// - Instance-specific and conditional handler execution
/// - Integration with event-scoped callback builders
/// - Automatic handler cleanup and lifecycle management
/// </summary>
public class LSDispatcher {
    /// <summary>
    /// Gets the singleton instance of the dispatcher.
    /// </summary>
    public static LSDispatcher Singleton { get; } = new LSDispatcher();

    /// <summary>
    /// Initializes a new instance of the LSDispatcher.
    /// </summary>
    public LSDispatcher() { }

    private readonly ConcurrentDictionary<System.Type, List<LSHandlerRegistration>> _handlers = new();
    private readonly object _lock = new object();

    /// <summary>
    /// Logs an action or state change for an event with structured data.
    /// </summary>
    private void logEventAction<TEvent>(TEvent @event, string action, string details, object? additionalData = null) where TEvent : ILSEvent {
        if (@event is LSBaseEvent baseEvent) {
            var timestamp = System.DateTime.UtcNow;
            var logKey = $"log.{timestamp:yyyyMMdd_HHmmss_fff}";
            
            var logEntry = new Dictionary<string, object> {
                ["timestamp"] = timestamp,
                ["action"] = action,
                ["details"] = details,
                ["phase"] = baseEvent.CurrentPhase.ToString(),
                ["cancelled"] = @event.IsCancelled,
                ["waiting"] = baseEvent.IsWaiting,
                ["completed"] = @event.IsCompleted,
                ["hasFailures"] = @event.HasFailures
            };
            
            if (additionalData != null) {
                logEntry["data"] = additionalData;
            }
            
            baseEvent.SetData(logKey, logEntry);
        }
    }

    #region Event Registration

    /// <summary>
    /// Creates a fluent registration builder for the specified event type.
    /// This is the recommended way to register handlers with complex configurations.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register handlers for.</typeparam>
    /// <returns>A registration builder that allows fluent configuration of the handler.</returns>
    public LSEventHandlerBuilder<TEvent> ForEvent<TEvent>() where TEvent : ILSEvent {
        return new LSEventHandlerBuilder<TEvent>(this);
    }

    /// <summary>
    /// Internal registration method with all available options. Used by the fluent registration API
    /// and can be called directly for maximum control.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register the handler for.</typeparam>
    /// <param name="handler">The handler function to execute.</param>
    /// <param name="phase">The phase in which to execute the handler.</param>
    /// <param name="priority">The execution priority within the phase.</param>
    /// <param name="instanceType">The type of instance to restrict the handler to, if any.</param>
    /// <param name="instance">The specific instance to restrict the handler to, if any.</param>
    /// <param name="maxExecutions">Maximum number of executions allowed (-1 for unlimited).</param>
    /// <param name="condition">Optional condition that must be met for execution.</param>
    /// <returns>A unique identifier for the registered handler.</returns>
    internal System.Guid registerHandler<TEvent>(
        LSPhaseHandler<TEvent> handler,
        LSEventPhase phase,
        LSPhasePriority priority,
        System.Type? instanceType,
        object? instance,
        int maxExecutions,
        LSEventCondition<TEvent>? condition
    ) where TEvent : ILSEvent {
        var registration = new LSHandlerRegistration {
            Id = System.Guid.NewGuid(),
            EventType = typeof(TEvent),
            Handler = (evt, ctx) => handler((TEvent)evt, ctx),
            Phase = phase,
            Priority = priority,
            InstanceType = instanceType,
            Instance = instance,
            MaxExecutions = maxExecutions,
            Condition = condition != null ? evt => condition((TEvent)evt) : null,
            ExecutionCount = 0
        };

        lock (_lock) {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) {
                list = new List<LSHandlerRegistration>();
                _handlers[typeof(TEvent)] = list;
            }
            list.Add(registration);
            // Sort by priority to ensure correct execution order
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        return registration.Id;
    }

    #endregion

    #region Event Processing

    /// <summary>
    /// Processes an event through all phases in the correct order.
    /// The event will go through VALIDATE, PREPARE, EXECUTE, FINALIZE, CANCEL (if cancelled), and COMPLETE phases.
    /// If the event is aborted in any phase (except COMPLETE), the CANCEL phase will run followed by the COMPLETE phase for cleanup.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to process.</typeparam>
    /// <param name="event">The event instance to process.</param>
    /// <returns>True if the event completed successfully, false if it was aborted.</returns>
    internal bool processEvent<TEvent>(TEvent @event) where TEvent : ILSEvent {
        return processEventInternal(@event, resuming: false);
    }

    /// <summary>
    /// Resumes processing of an event that was paused in a WAITING state.
    /// This method should be called after the async operation completes and
    /// Resume() or Abort() has been called on the event.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to resume processing for.</typeparam>
    /// <param name="event">The event instance to resume processing.</param>
    /// <returns>True if the event completed successfully, false if it was aborted.</returns>
    internal bool continueProcessing<TEvent>(TEvent @event) where TEvent : ILSEvent {
        logEventAction(@event, "ContinueProcessing", "Resuming event processing");
        return processEventInternal(@event, resuming: true);
    }

    private bool processEventInternal<TEvent>(TEvent @event, bool resuming) where TEvent : ILSEvent {
        logEventAction(@event, "ProcessEventInternal", $"Starting processing (resuming: {resuming})");
        
        // Early exit conditions
        if (@event.IsCompleted) {
            logEventAction(@event, "ProcessEventInternal", "Event already completed");
            return !@event.IsCancelled;
        }
        
        if (@event.IsCancelled && !resuming) {
            logEventAction(@event, "ProcessEventInternal", "Event already cancelled and not resuming");
            return false;
        }

        var mutableEvent = (ILSMutableEvent)@event;

        // Handle resumption logic
        if (resuming) {
            return handleEventResumption(@event);
        }

        // Process event through all phases
        return executeEventLifecycle(@event);
    }

    private bool handleEventResumption<TEvent>(TEvent @event) where TEvent : ILSEvent {
        var mutableEvent = (ILSMutableEvent)@event;
        
        if (mutableEvent.IsWaiting) {
            logEventAction(@event, "HandleEventResumption", "Event still waiting, cannot resume");
            return false;
        }

        logEventAction(@event, "HandleEventResumption", $"Resuming from phase {mutableEvent.CurrentPhase}");

        // Determine the appropriate phase to jump to based on event state
        var targetPhase = determineResumptionPhase(@event);
        
        // Execute from the target phase onwards
        return executeFromPhase(@event, targetPhase);
    }

    private LSEventPhase determineResumptionPhase<TEvent>(TEvent @event) where TEvent : ILSEvent {
        var mutableEvent = (ILSMutableEvent)@event;
        
        // If event was cancelled during async operation, go to CANCEL phase
        if (@event.IsCancelled && mutableEvent.CurrentPhase != LSEventPhase.CANCEL && mutableEvent.CurrentPhase != LSEventPhase.COMPLETE) {
            logEventAction(@event, "DetermineResumptionPhase", "Event cancelled, jumping to CANCEL phase");
            return LSEventPhase.CANCEL;
        }
        
        // If event has failures during async operation, go to FAILURE phase  
        if (@event.HasFailures && mutableEvent.CurrentPhase != LSEventPhase.FAILURE && 
            mutableEvent.CurrentPhase != LSEventPhase.CANCEL && mutableEvent.CurrentPhase != LSEventPhase.COMPLETE) {
            logEventAction(@event, "DetermineResumptionPhase", "Event has failures, jumping to FAILURE phase");
            return LSEventPhase.FAILURE;
        }
        
        // Otherwise, continue from the next phase after current
        var currentPhase = mutableEvent.CurrentPhase;
        var phases = getPhaseExecutionOrder();
        var currentIndex = System.Array.IndexOf(phases, currentPhase);
        
        if (currentIndex >= 0 && currentIndex < phases.Length - 1) {
            var nextPhase = phases[currentIndex + 1];
            logEventAction(@event, "DetermineResumptionPhase", $"Continuing to next phase: {nextPhase}");
            return nextPhase;
        }
        
        logEventAction(@event, "DetermineResumptionPhase", "No next phase, going to COMPLETE");
        return LSEventPhase.COMPLETE;
    }

    private bool executeEventLifecycle<TEvent>(TEvent @event) where TEvent : ILSEvent {
        logEventAction(@event, "ExecuteEventLifecycle", "Starting full event lifecycle");
        return executeFromPhase(@event, LSEventPhase.VALIDATE);
    }

    private bool executeFromPhase<TEvent>(TEvent @event, LSEventPhase startPhase) where TEvent : ILSEvent {
        var phases = getPhaseExecutionOrder();
        var startIndex = System.Array.IndexOf(phases, startPhase);
        
        if (startIndex == -1) {
            logEventAction(@event, "ExecuteFromPhase", $"Invalid start phase: {startPhase}");
            startIndex = 0;
        }

        for (int i = startIndex; i < phases.Length; i++) {
            var phase = phases[i];
            
            // Check if phase should be skipped based on event state
            if (shouldSkipPhase(@event, phase)) {
                logEventAction(@event, "ExecuteFromPhase", $"Skipping phase {phase}");
                continue;
            }

            logEventAction(@event, "ExecuteFromPhase", $"Executing phase {phase}");
            var result = executePhase(@event, phase);
            
            // Handle phase execution result
            if (!processPhaseResult(@event, phase, result)) {
                logEventAction(@event, "ExecuteFromPhase", $"Phase {phase} terminated event processing");
                return false;
            }
            
            // Mark phase as completed
            var mutableEvent = (ILSMutableEvent)@event;
            mutableEvent.CompletedPhases |= phase;
            logEventAction(@event, "ExecuteFromPhase", $"Phase {phase} completed");
        }

        // Event completed successfully
        var finalMutableEvent = (ILSMutableEvent)@event;
        finalMutableEvent.IsCompleted = true;
        logEventAction(@event, "ExecuteFromPhase", "Event lifecycle completed", new { success = !@event.IsCancelled });
        
        return !@event.IsCancelled;
    }

    private LSEventPhase[] getPhaseExecutionOrder() {
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

    private bool shouldSkipPhase<TEvent>(TEvent @event, LSEventPhase phase) where TEvent : ILSEvent {
        var shouldSkip = phase switch {
            // Skip SUCCESS if event is cancelled or has failures
            LSEventPhase.SUCCESS => @event.IsCancelled || @event.HasFailures,
            
            // Skip FAILURE if event doesn't have failures OR if event is cancelled
            LSEventPhase.FAILURE => !@event.HasFailures || @event.IsCancelled,
            
            // Skip CANCEL if event is not cancelled
            LSEventPhase.CANCEL => !@event.IsCancelled,
            
            // If event is cancelled, skip phases before CANCEL (except CANCEL and COMPLETE)
            _ when @event.IsCancelled => phase != LSEventPhase.CANCEL && phase != LSEventPhase.COMPLETE,
            
            // If event has failures but is not cancelled, skip SUCCESS phase
            _ when @event.HasFailures => phase == LSEventPhase.SUCCESS,
            
            // Default: don't skip
            _ => false
        };
        
        logEventAction(@event, "ShouldSkipPhase", $"Phase {phase} skip decision: {shouldSkip}", 
            new { IsCancelled = @event.IsCancelled, HasFailures = @event.HasFailures });
        
        return shouldSkip;
    }

    private bool processPhaseResult<TEvent>(TEvent @event, LSEventPhase phase, LSPhaseExecutionResult result) where TEvent : ILSEvent {
        var mutableEvent = (ILSMutableEvent)@event;
        
        if (result.ShouldWait) {
            logEventAction(@event, "ProcessPhaseResult", $"Phase {phase} requested waiting");
            return false; // Event is waiting, processing will resume later
        }
        
        if (result.ShouldCancel) {
            logEventAction(@event, "ProcessPhaseResult", $"Phase {phase} requested cancellation", new { reason = result.ErrorMessage });
            mutableEvent.IsCancelled = true;
            if (!string.IsNullOrEmpty(result.ErrorMessage) && @event is LSBaseEvent baseEvent) {
                baseEvent.SetErrorMessage(result.ErrorMessage);
            }
            // Continue processing to allow CANCEL phase to run
            return true;
        }
        
        if (result.HasFailures) {
            logEventAction(@event, "ProcessPhaseResult", $"Phase {phase} reported failures", new { reason = result.ErrorMessage });
            mutableEvent.HasFailures = true;
            if (!string.IsNullOrEmpty(result.ErrorMessage) && @event is LSBaseEvent baseEvent) {
                baseEvent.SetErrorMessage(result.ErrorMessage);
            }
            // Continue processing to allow FAILURE phase to run
            return true;
        }
        
        return result.Success;
    }

    private LSPhaseExecutionResult? processDeferredResumption<TEvent>(TEvent @event) where TEvent : ILSEvent {
        if (@event is LSBaseEvent baseEvent) {
            var deferredResumption = baseEvent.GetDeferredResumption();
            if (deferredResumption.HasValue) {
                logEventAction(@event, "ProcessDeferredResumption", $"Processing deferred resumption: {deferredResumption.Value}");
                
                baseEvent.ClearDeferredResumption();
                var mutableEvent = (ILSMutableEvent)@event;
                mutableEvent.IsWaiting = false;

                return deferredResumption.Value switch {
                    LSBaseEvent.ResumptionType.Resume => LSPhaseExecutionResult.Successful(),
                    LSBaseEvent.ResumptionType.Abort => LSPhaseExecutionResult.Cancelled("Deferred abort processed"),
                    LSBaseEvent.ResumptionType.Fail => LSPhaseExecutionResult.Failed("Deferred failure processed"),
                    _ => LSPhaseExecutionResult.Successful()
                };
            }
        }
        return null;
    }

    private LSPhaseExecutionResult executeHandler<TEvent>(TEvent @event, LSHandlerRegistration handler, LSEventPhase phase, 
        System.DateTime startTime, List<string> errors) where TEvent : ILSEvent {
        try {
            var elapsed = System.DateTime.UtcNow - startTime;
            var context = new LSPhaseContext(phase, handler.Priority, elapsed, 0, errors);

            logEventAction(@event, "ExecuteHandler", $"Executing handler {handler.Id} in phase {phase}");
            
            var result = handler.Handler(@event, context);
            handler.ExecutionCount++;

            logEventAction(@event, "ExecuteHandler", $"Handler {handler.Id} in phase {phase} returned {result}");

            return result switch {
                LSHandlerResult.CONTINUE => LSPhaseExecutionResult.Successful(),
                LSHandlerResult.SKIP_REMAINING => LSPhaseExecutionResult.Successful(),
                LSHandlerResult.CANCEL => LSPhaseExecutionResult.Cancelled(
                    @event is LSBaseEvent baseEvent && !string.IsNullOrEmpty(baseEvent.ErrorMessage) 
                        ? baseEvent.ErrorMessage 
                        : $"Handler {handler.Id} cancelled event"),
                LSHandlerResult.FAILURE => LSPhaseExecutionResult.Failed(
                    @event is LSBaseEvent baseEvent && !string.IsNullOrEmpty(baseEvent.ErrorMessage) 
                        ? baseEvent.ErrorMessage 
                        : $"Handler {handler.Id} reported failure"),
                LSHandlerResult.WAITING => LSPhaseExecutionResult.Waiting(),
                LSHandlerResult.RETRY => handleRetryLogic(@event, handler, context, errors),
                _ => LSPhaseExecutionResult.Successful()
            };
        }
        catch (LSException ex) {
            var errorMsg = $"Handler {handler.Id} in phase {phase} failed with LSException: {ex.Message}";
            errors.Add(errorMsg);
            logEventAction(@event, "ExecuteHandler", errorMsg);
            
            // Critical validation failures should stop processing
            if (phase == LSEventPhase.VALIDATE && handler.Priority == LSPhasePriority.CRITICAL) {
                return LSPhaseExecutionResult.Cancelled("Critical validation failure");
            }
            
            return LSPhaseExecutionResult.Successful(); // Continue with other handlers
        }
        catch (System.Exception ex) {
            var errorMsg = $"Handler {handler.Id} in phase {phase} failed with unexpected exception: {ex.Message}";
            errors.Add(errorMsg);
            logEventAction(@event, "ExecuteHandler", errorMsg);
            
            // Critical validation failures should stop processing
            if (phase == LSEventPhase.VALIDATE && handler.Priority == LSPhasePriority.CRITICAL) {
                return LSPhaseExecutionResult.Cancelled("Critical validation failure");
            }
            
            // Re-throw exceptions that aren't handled by the event system
            // This allows tests and client code to catch exceptions as expected
            throw;
        }
    }

    private LSPhaseExecutionResult handleRetryLogic<TEvent>(TEvent @event, LSHandlerRegistration handler, 
        LSPhaseContext context, List<string> errors) where TEvent : ILSEvent {
        if (handler.ExecutionCount < 3) {
            try {
                logEventAction(@event, "HandleRetryLogic", $"Retrying handler {handler.Id} (attempt {handler.ExecutionCount})");
                var retryResult = handler.Handler(@event, context);
                
                if (retryResult == LSHandlerResult.CANCEL) {
                    return LSPhaseExecutionResult.Cancelled($"Handler {handler.Id} cancelled on retry");
                }
                
                return LSPhaseExecutionResult.Successful();
            }
            catch (LSException ex) {
                errors.Add($"Handler {handler.Id} retry failed: {ex.Message}");
                return LSPhaseExecutionResult.Failed($"Handler {handler.Id} retry failed");
            }
        }
        
        logEventAction(@event, "HandleRetryLogic", $"Handler {handler.Id} exceeded retry limit");
        return LSPhaseExecutionResult.Failed($"Handler {handler.Id} exceeded retry limit");
    }

    private LSPhaseExecutionResult executePhase<TEvent>(TEvent @event, LSEventPhase phase) where TEvent : ILSEvent {
        var mutableEvent = (ILSMutableEvent)@event;
        mutableEvent.CurrentPhase = phase;
        
        logEventAction(@event, "ExecutePhase", $"Starting phase {phase}");

        var type = @event.GetType();
        if (!_handlers.TryGetValue(type, out var allHandlers)) {
            logEventAction(@event, "ExecutePhase", $"No handlers registered for {type.Name} in phase {phase}");
            return LSPhaseExecutionResult.Successful();
        }

        // Filter handlers for this phase
        var phaseHandlers = allHandlers
            .Where(h => h.Phase == phase)
            .Where(h => h.MaxExecutions == -1 || h.ExecutionCount < h.MaxExecutions)
            .Where(h => matchesInstance(h, @event))
            .Where(h => h.Condition == null || h.Condition(@event))
            .ToList();

        if (!phaseHandlers.Any()) {
            logEventAction(@event, "ExecutePhase", $"No matching handlers for phase {phase}");
            return LSPhaseExecutionResult.Successful();
        }

        logEventAction(@event, "ExecutePhase", $"Executing {phaseHandlers.Count} handlers for phase {phase}");

        // Execute all handlers for this phase
        var startTime = System.DateTime.UtcNow;
        var errors = new List<string>();
        bool hasWaitingHandler = false;
        
        foreach (var handler in phaseHandlers) {
            var handlerResult = executeHandler(@event, handler, phase, startTime, errors);
            
            // Handle different handler results
            switch (handlerResult) {
                case LSPhaseExecutionResult { ShouldWait: true }:
                    logEventAction(@event, "ExecutePhase", $"Handler requested waiting in phase {phase}");
                    mutableEvent.IsWaiting = true;
                    hasWaitingHandler = true;
                    break;
                    
                case LSPhaseExecutionResult { ShouldCancel: true }:
                    logEventAction(@event, "ExecutePhase", $"Handler requested cancellation in phase {phase}");
                    mutableEvent.IsCancelled = true;
                    if (!string.IsNullOrEmpty(handlerResult.ErrorMessage) && @event is LSBaseEvent baseEvent) {
                        baseEvent.SetErrorMessage(handlerResult.ErrorMessage);
                    }
                    // Continue with other handlers in this phase, then proceed to CANCEL/COMPLETE phases
                    break;
                    
                case LSPhaseExecutionResult { HasFailures: true }:
                    logEventAction(@event, "ExecutePhase", $"Handler reported failure in phase {phase}");
                    // Continue with other handlers but track the failure
                    mutableEvent.HasFailures = true;
                    break;
                    
                case LSPhaseExecutionResult { Success: false }:
                    logEventAction(@event, "ExecutePhase", $"Handler failed in phase {phase}");
                    return handlerResult;
            }
        }

        // After all handlers have executed, check for deferred resumption
        if (hasWaitingHandler) {
            var deferredResult = processDeferredResumption(@event);
            if (deferredResult.HasValue) {
                logEventAction(@event, "ExecutePhase", $"Deferred resumption processed: {deferredResult.Value.ShouldCancel}, {deferredResult.Value.HasFailures}");
                
                // Apply the deferred state changes to the event
                if (deferredResult.Value.ShouldCancel) {
                    mutableEvent.IsCancelled = true;
                }
                if (deferredResult.Value.HasFailures) {
                    mutableEvent.HasFailures = true;
                }
                
                // Return successful completion of this phase - the lifecycle will handle the state transitions
                return LSPhaseExecutionResult.Successful();
            } else {
                // Still waiting for async operation
                return LSPhaseExecutionResult.Waiting();
            }
        }

        if (errors.Any()) {
            logEventAction(@event, "ExecutePhase", $"Phase {phase} completed with errors", new { errorCount = errors.Count, errors });
            return LSPhaseExecutionResult.Failed($"Phase {phase} had {errors.Count} errors");
        }

        logEventAction(@event, "ExecutePhase", $"Phase {phase} completed successfully");
        return LSPhaseExecutionResult.Successful();
    }

    #endregion

    #region Handler Management

    /// <summary>
    /// Checks if a handler's instance restriction matches the current event.
    /// This is used to filter handlers that are registered for specific instances.
    /// </summary>
    /// <typeparam name="TEvent">The type of event being processed.</typeparam>
    /// <param name="handler">The handler registration to check.</param>
    /// <param name="event">The event being processed.</param>
    /// <returns>True if the handler should execute for this event.</returns>
    private bool matchesInstance<TEvent>(LSHandlerRegistration handler, TEvent @event) where TEvent : ILSEvent {
        if (handler.Instance == null) {
            return true; // Static handler matches all events
        }

        // For LSEvent<T> instances, check if the Instance property matches
        if (@event is LSBaseEvent baseEvent) {
            var instanceProperty = baseEvent.GetType().GetProperty("Instance");
            if (instanceProperty != null) {
                var eventInstance = instanceProperty.GetValue(baseEvent);
                return ReferenceEquals(eventInstance, handler.Instance);
            }
        }

        return false;
    }

    /// <summary>
    /// Unregisters a handler by its unique identifier.
    /// </summary>
    /// <param name="handlerId">The unique identifier of the handler to unregister.</param>
    /// <returns>True if the handler was found and removed, false otherwise.</returns>
    public bool UnregisterHandler(System.Guid handlerId) {
        lock (_lock) {
            foreach (var list in _handlers.Values) {
                var handler = list.FirstOrDefault(h => h.Id == handlerId);
                if (handler != null) {
                    list.Remove(handler);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Unregisters handlers based on specified criteria.
    /// This is used internally by the fluent unregistration API.
    /// </summary>
    /// <typeparam name="TEvent">The event type to unregister handlers for.</typeparam>
    /// <param name="phase">The phase filter.</param>
    /// <param name="priority">The priority filter.</param>
    /// <param name="instanceType">Optional instance type filter.</param>
    /// <param name="instance">Optional specific instance filter.</param>
    /// <param name="maxExecutions">The max executions filter.</param>
    /// <param name="condition">Optional condition filter.</param>
    /// <returns>The number of handlers that were removed.</returns>
    internal int unregisterHandlers<TEvent>(
        LSEventPhase phase,
        LSPhasePriority priority,
        System.Type? instanceType,
        object? instance,
        int maxExecutions,
        LSEventCondition<TEvent>? condition
    ) where TEvent : ILSEvent {

        lock (_lock) {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) {
                return 0;
            }

            var toRemove = list.Where(handler => {
                // Match all the configured criteria
                if (handler.Phase != phase) return false;
                if (handler.Priority != priority) return false;
                if (instanceType != null && handler.InstanceType != instanceType) return false;
                if (instance != null && !ReferenceEquals(handler.Instance, instance)) return false;
                if (maxExecutions != -1 && handler.MaxExecutions != maxExecutions) return false;
                if (condition != null && !ReferenceEquals(handler.Condition, condition)) return false;

                return true;
            }).ToList();

            foreach (var handler in toRemove) {
                list.Remove(handler);
            }

            return toRemove.Count;
        }
    }

    /// <summary>
    /// Unregisters a specific handler based on criteria and handler reference.
    /// This is used internally by the fluent unregistration API.
    /// </summary>
    /// <typeparam name="TEvent">The event type to unregister handlers for.</typeparam>
    /// <param name="handlerToRemove">The specific handler function to unregister.</param>
    /// <param name="phase">The phase filter.</param>
    /// <param name="priority">The priority filter.</param>
    /// <param name="instanceType">Optional instance type filter.</param>
    /// <param name="instance">Optional specific instance filter.</param>
    /// <param name="maxExecutions">The max executions filter.</param>
    /// <param name="condition">Optional condition filter.</param>
    /// <returns>The number of handlers that were removed (0 or 1).</returns>
    internal int unregisterHandler<TEvent>(
        LSPhaseHandler<TEvent> handlerToRemove,
        LSEventPhase phase,
        LSPhasePriority priority,
        System.Type? instanceType,
        object? instance,
        int maxExecutions,
        LSEventCondition<TEvent>? condition
    ) where TEvent : ILSEvent {

        lock (_lock) {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) {
                return 0;
            }

            var toRemove = list.Where(handler => {
                // First check if this is the exact handler we're looking for
                if (!ReferenceEquals(handler.Handler, handlerToRemove)) return false;

                // Then match all the configured criteria
                if (handler.Phase != phase) return false;
                if (handler.Priority != priority) return false;
                if (instanceType != null && handler.InstanceType != instanceType) return false;
                if (instance != null && !ReferenceEquals(handler.Instance, instance)) return false;
                if (maxExecutions != -1 && handler.MaxExecutions != maxExecutions) return false;
                if (condition != null && !ReferenceEquals(handler.Condition, condition)) return false;

                return true;
            }).ToList();

            foreach (var handler in toRemove) {
                list.Remove(handler);
            }

            return toRemove.Count;
        }
    }

    /// <summary>
    /// Registers a batch of event-scoped handlers as a single unit for better performance.
    /// This method is used internally by the callback builder system.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register handlers for.</typeparam>
    /// <param name="batch">The batch containing the handlers to register.</param>
    /// <returns>A unique identifier for the registered batch that can be used for unregistration.</returns>
    internal System.Guid registerBatchedHandlers<TEvent>(LSEventCallbackBatch<TEvent> batch) where TEvent : ILSEvent {
        var batchId = System.Guid.NewGuid();

        // Register batch handlers for all phases that have handlers
        var phases = new[] {
            LSEventPhase.VALIDATE,
            LSEventPhase.PREPARE,
            LSEventPhase.EXECUTE,
            LSEventPhase.SUCCESS,
            LSEventPhase.FAILURE,
            LSEventPhase.CANCEL,
            LSEventPhase.COMPLETE
        };

        lock (_lock) {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) {
                list = new List<LSHandlerRegistration>();
                _handlers[typeof(TEvent)] = list;
            }

            foreach (var phase in phases) {
                // Only register for phases that have handlers in the batch
                if (batch.GetHandlersForPhase(phase).Any()) {
                    var batchedHandler = new LSHandlerRegistration {
                        Id = System.Guid.NewGuid(), // Each phase handler gets its own ID
                        EventType = typeof(TEvent),
                        Handler = (evt, ctx) => executeBatchedHandlers((TEvent)evt, ctx, batch),
                        Phase = phase,
                        Priority = LSPhasePriority.NORMAL,
                        Instance = null, // Don't use Instance field for event-scoped handlers
                        InstanceType = typeof(TEvent),
                        MaxExecutions = 1, // One-time execution
                        Condition = evt => ReferenceEquals(evt, batch.TargetEvent), // Use condition instead
                        ExecutionCount = 0
                    };

                    list.Add(batchedHandler);
                }
            }

            // Sort by priority to ensure correct execution order
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        return batchId;
    }

    /// <summary>
    /// Executes batched handlers for the current phase.
    /// </summary>
    /// <typeparam name="TEvent">The event type being processed.</typeparam>
    /// <param name="evt">The event instance being processed.</param>
    /// <param name="ctx">The current phase context.</param>
    /// <param name="batch">The batch containing the handlers to execute.</param>
    /// <returns>The result of the batch execution.</returns>
    private LSHandlerResult executeBatchedHandlers<TEvent>(TEvent evt, LSPhaseContext ctx, LSEventCallbackBatch<TEvent> batch)
        where TEvent : ILSEvent {

        var result = LSHandlerResult.CONTINUE;

        // Execute only handlers for the current phase
        var phaseHandlers = batch.GetHandlersForPhase(ctx.CurrentPhase);

        foreach (var handler in phaseHandlers) {
            try {
                // Check if event was cancelled (e.g., via immediate Abort() call)
                // BUT allow CANCEL and COMPLETE phase handlers to run even when event is cancelled
                if (evt.IsCancelled && ctx.CurrentPhase != LSEventPhase.CANCEL && ctx.CurrentPhase != LSEventPhase.COMPLETE) {
                    return LSHandlerResult.CANCEL; // Stop processing immediately
                }

                // Check handler condition if present
                if (handler.Condition?.Invoke(evt) ?? true) {
                    var handlerResult = handler.Handler(evt, ctx);

                    // Update result based on handler outcome
                    switch (handlerResult) {
                        case LSHandlerResult.CONTINUE:
                            continue;
                        case LSHandlerResult.SKIP_REMAINING:
                            return handlerResult; // Skip remaining handlers in this batch
                        case LSHandlerResult.CANCEL:
                            return handlerResult; // Cancel entire event processing
                        case LSHandlerResult.WAITING:
                            // Handler requested to wait for async operation
                            var mutableEvent = (ILSMutableEvent)evt;
                            mutableEvent.IsWaiting = true;

                            // Check if async operation already completed (scenario 2: immediate completion)
                            if (evt is LSBaseEvent baseEvent) {
                                var deferredResumption = baseEvent.GetDeferredResumption();
                                if (deferredResumption.HasValue) {
                                    // Async operation completed before we set IsWaiting = true
                                    baseEvent.ClearDeferredResumption();
                                    mutableEvent.IsWaiting = false;

                                    // Process the deferred action
                                    switch (deferredResumption.Value) {
                                        case LSBaseEvent.ResumptionType.Resume:
                                            continue; // Continue with next handler
                                        case LSBaseEvent.ResumptionType.Abort:
                                            return LSHandlerResult.CANCEL; // Cancel entire event processing
                                        case LSBaseEvent.ResumptionType.Fail:
                                            mutableEvent.HasFailures = true;
                                            continue; // Continue with next handler
                                    }
                                } else {
                                    // Normal scenario 1: async operation is still pending
                                    baseEvent.SetData("waiting.phase", ctx.CurrentPhase.ToString());
                                    baseEvent.SetData("waiting.handler.id", System.Guid.NewGuid().ToString()); // Batch handler doesn't have individual ID
                                    baseEvent.SetData("waiting.started.at", System.DateTime.UtcNow);
                                    baseEvent.SetData("waiting.handler.count", 0); // Within batch

                                    return handlerResult; // Return WAITING to signal pause
                                }
                            } else {
                                // Non-LSBaseEvent - use original behavior
                                return handlerResult; // Return WAITING to signal pause
                            }
                            break;
                        case LSHandlerResult.FAILURE:
                            // Mark event as failed but continue with remaining handlers
                            var mutableEvt = (ILSMutableEvent)evt;
                            mutableEvt.HasFailures = true;
                            if (evt is LSBaseEvent failureEvent) {
                                failureEvent.SetData("batch.failure.phase", ctx.CurrentPhase.ToString());
                                failureEvent.SetData("batch.failure.time", System.DateTime.UtcNow);
                            }
                            continue;
                        case LSHandlerResult.RETRY:
                            // For batch handlers, we don't support retry at individual handler level
                            // Convert to continue to avoid infinite loops
                            continue;
                    }
                }
            } catch (LSException ex) {
                // Log error but continue with remaining handlers unless it's critical validation
                if (ctx.CurrentPhase == LSEventPhase.VALIDATE && handler.Priority == LSPhasePriority.CRITICAL) {
                    evt.SetData("batch.error", ex.Message);
                    return LSHandlerResult.CANCEL;
                }

                // For non-critical errors, log and continue
                evt.SetData($"batch.error.{ctx.CurrentPhase}.{handler.Priority}", ex.Message);
            } catch (System.Exception ex) {
                // For unexpected exceptions in batched handlers, re-throw to allow test frameworks to catch them
                evt.SetData($"batch.exception.{ctx.CurrentPhase}.{handler.Priority}", ex.Message);
                throw; // Re-throw the exception for test scenarios and proper error handling
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the number of registered handlers for a specific event type.
    /// Useful for debugging and monitoring.
    /// </summary>
    /// <typeparam name="TEvent">The event type to count handlers for.</typeparam>
    /// <returns>The number of registered handlers.</returns>
    public int GetHandlerCount<TEvent>() where TEvent : ILSEvent {
        return _handlers.TryGetValue(typeof(TEvent), out var list) ? list.Count : 0;
    }

    /// <summary>
    /// Gets the total number of registered handlers across all event types.
    /// Useful for system monitoring and diagnostics.
    /// </summary>
    /// <returns>The total number of registered handlers.</returns>
    public int GetTotalHandlerCount() {
        return _handlers.Values.Sum(list => list.Count);
    }

    #endregion
}
