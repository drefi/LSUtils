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
public class LSLegacyDispatcher {
    /// <summary>
    /// Gets the singleton instance of the dispatcher.
    /// </summary>
    public static LSLegacyDispatcher Singleton { get; } = new LSLegacyDispatcher();

    /// <summary>
    /// Initializes a new instance of the LSDispatcher.
    /// </summary>
    public LSLegacyDispatcher() { }

    private readonly ConcurrentDictionary<System.Type, List<LSLegacyHandlerRegistration>> _handlers = new();
    private readonly object _lock = new object();

    /// <summary>
    /// Logs an action or state change for an event with structured data for debugging and monitoring.
    /// This method automatically tracks event processing steps with timestamps and contextual information.
    /// </summary>
    /// <typeparam name="TEvent">The type of event being logged.</typeparam>
    /// <param name="event">The event instance to log information for.</param>
    /// <param name="action">The action being performed (e.g., "ExecutePhase", "HandlerResult").</param>
    /// <param name="details">Detailed description of what occurred.</param>
    /// <param name="additionalData">Optional structured data to include in the log entry.</param>
    private void logEventAction<TEvent>(TEvent @event, string action, string details, object? additionalData = null) where TEvent : ILSEvent {
        if (@event is LSLegacyBaseEvent baseEvent) {
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
    public LSLegacyEventHandlerBuilder<TEvent> ForEvent<TEvent>() where TEvent : ILSEvent {
        return new LSLegacyEventHandlerBuilder<TEvent>(this);
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
        LSLegacyPhaseHandler<TEvent> handler,
        LSLegacyEventPhase phase,
        LSPriority priority,
        System.Type? instanceType,
        object? instance,
        int maxExecutions,
        LSLegacyEventCondition<TEvent>? condition
    ) where TEvent : ILSEvent {
        var registration = new LSLegacyHandlerRegistration {
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
                list = new List<LSLegacyHandlerRegistration>();
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
    /// The event will go through VALIDATE → PREPARE → EXECUTE → SUCCESS/FAILURE/CANCEL → COMPLETE phases.
    /// If the event is aborted in any phase (except COMPLETE), the CANCEL phase will run followed by the COMPLETE phase for cleanup.
    /// 
    /// ⚠️ INTERNAL USE ONLY: This method is internal to prevent external code from bypassing proper event processing flow.
    /// External code should use event.Dispatch(dispatcher) or event.WithCallbacks(dispatcher).Dispatch() instead.
    /// 
    /// The dispatcher automatically handles:
    /// - Phase sequencing and flow control
    /// - Handler filtering and execution order  
    /// - Async operation management (WAITING/Resume/Abort/Fail)
    /// - Error handling and cleanup
    /// - Integration between global and event-scoped handlers
    /// </summary>
    /// <typeparam name="TEvent">The type of event to process.</typeparam>
    /// <param name="event">The event instance to process.</param>
    /// <returns>
    /// True if the event completed successfully through all phases,
    /// false if it was cancelled, had critical failures, or is waiting for async operations.
    /// </returns>
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

        var mutableEvent = (ILSLegacyMutableEvent)@event;

        // Handle resumption logic
        if (resuming) {
            return handleEventResumption(@event);
        }

        // Process event through all phases
        return executeEventLifecycle(@event);
    }

    private bool handleEventResumption<TEvent>(TEvent @event) where TEvent : ILSEvent {
        var mutableEvent = (ILSLegacyMutableEvent)@event;

        if (mutableEvent.IsWaiting) {
            logEventAction(@event, "HandleEventResumption", "Event still waiting, cannot resume");
            return false;
        }

        //logEventAction(@event, "HandleEventResumption", $"Resuming from phase {mutableEvent.CurrentPhase}");

        // Determine the appropriate phase to jump to based on event state
        var targetPhase = determineResumptionPhase(@event);

        // Execute from the target phase onwards
        return executeFromPhase(@event, targetPhase);
    }

    private LSLegacyEventPhase determineResumptionPhase<TEvent>(TEvent @event) where TEvent : ILSEvent {
        var mutableEvent = (ILSLegacyMutableEvent)@event;

        // If event was cancelled during async operation, go to CANCEL phase
        if (@event.IsCancelled) {
            logEventAction(@event, "DetermineResumptionPhase", "Event cancelled, jumping to CANCEL phase");
            return LSLegacyEventPhase.CANCEL;
        }

        // If event has failures during async operation, go to FAILURE phase
        if (@event.HasFailures) {
            logEventAction(@event, "DetermineResumptionPhase", "Event has failures, jumping to FAILURE phase");
            return LSLegacyEventPhase.FAILURE;
        }
        //LEGACY CODE, WILL BE REPLACED
        // Otherwise, continue from the next phase after current
        //var currentPhase = mutableEvent.CurrentPhase;
        var phases = LSLegacyPhaseManager.GetPhaseExecutionOrder();
        //var currentIndex = System.Array.IndexOf(phases, currentPhase);

        // if (currentIndex >= 0 && currentIndex < phases.Length - 1) {
        //     var nextPhase = phases[currentIndex + 1];
        //     logEventAction(@event, "DetermineResumptionPhase", $"Continuing to next phase: {nextPhase}");
        //     return nextPhase;
        // }

        logEventAction(@event, "DetermineResumptionPhase", "No next phase, going to COMPLETE");
        return LSLegacyEventPhase.COMPLETE;
    }

    private bool executeEventLifecycle<TEvent>(TEvent @event) where TEvent : ILSEvent {
        logEventAction(@event, "ExecuteEventLifecycle", "Starting full event lifecycle");
        return executeFromPhase(@event, LSLegacyEventPhase.VALIDATE);
    }

    private bool executeFromPhase<TEvent>(TEvent @event, LSLegacyEventPhase startPhase) where TEvent : ILSEvent {
        var phases = LSLegacyPhaseManager.GetPhaseExecutionOrder();
        var startIndex = System.Array.IndexOf(phases, startPhase);

        if (startIndex == -1) {
            logEventAction(@event, "ExecuteFromPhase", $"Invalid start phase: {startPhase}");
            startIndex = 0;
        }

        return executePhaseSequence(@event, phases, startIndex);
    }

    private bool executePhaseSequence<TEvent>(TEvent @event, LSLegacyEventPhase[] phases, int startIndex) where TEvent : ILSEvent {
        for (int i = startIndex; i < phases.Length; i++) {
            var phase = phases[i];

            // Check if phase should be skipped based on event state
            if (LSLegacyPhaseManager.ShouldSkipPhase(@event, phase)) {
                logEventAction(@event, "ExecutePhaseSequence", $"Skipping phase {phase}");
                continue;
            }

            logEventAction(@event, "ExecutePhaseSequence", $"Executing phase {phase}");
            var result = executePhase(@event, phase);

            if (!LSLegacyPhaseManager.ProcessPhaseResult(@event, phase, result)) {
                logEventAction(@event, "ExecutePhaseSequence", $"Phase {phase} requested stop, ending execution");
                // If the event is waiting, return false to indicate processing is not complete
                var eventMutable = (ILSLegacyMutableEvent)@event;
                if (eventMutable.IsWaiting) {
                    return false;
                }
                return !@event.IsCancelled;
            }

            // Mark phase as completed
            var mutableEvent = (ILSLegacyMutableEvent)@event;
            //mutableEvent.CompletedPhases |= phase;
            logEventAction(@event, "ExecutePhaseSequence", $"Phase {phase} completed");
        }

        // Event completed successfully
        var finalMutableEvent = (ILSLegacyMutableEvent)@event;
        finalMutableEvent.IsCompleted = true;
        logEventAction(@event, "ExecutePhaseSequence", "Event lifecycle completed", new { success = !@event.IsCancelled });

        return !@event.IsCancelled;
    }



    private LSLegacyPhaseExecutionResult? processDeferredResumption<TEvent>(TEvent @event) where TEvent : ILSEvent {
        if (@event is LSLegacyBaseEvent baseEvent) {
            var deferredResumption = baseEvent.GetDeferredResumption();
            if (deferredResumption.HasValue) {
                logEventAction(@event, "ProcessDeferredResumption", $"Processing deferred resumption: {deferredResumption.Value}");

                baseEvent.ClearDeferredResumption();
                var mutableEvent = (ILSLegacyMutableEvent)@event;
                mutableEvent.IsWaiting = false;

                return deferredResumption.Value switch {
                    LSLegacyBaseEvent.ResumptionType.Resume => LSLegacyPhaseExecutionResult.Successful(),
                    LSLegacyBaseEvent.ResumptionType.Abort => LSLegacyPhaseExecutionResult.Cancelled("Deferred abort processed"),
                    LSLegacyBaseEvent.ResumptionType.Fail => LSLegacyPhaseExecutionResult.Failed("Deferred failure processed"),
                    _ => LSLegacyPhaseExecutionResult.Successful()
                };
            }
        }
        return null;
    }



    private LSLegacyPhaseExecutionResult executePhase<TEvent>(TEvent @event, LSLegacyEventPhase phase) where TEvent : ILSEvent {
        var mutableEvent = (ILSLegacyMutableEvent)@event;
        //mutableEvent.CurrentPhase = phase;

        logEventAction(@event, "ExecutePhase", $"Starting phase {phase}");

        var type = @event.GetType();
        if (!_handlers.TryGetValue(type, out var allHandlers)) {
            logEventAction(@event, "ExecutePhase", $"No handlers registered for {type.Name} in phase {phase}");
            return LSLegacyPhaseExecutionResult.Successful();
        }

        // Filter handlers for this phase using utility class
        var phaseHandlers = LSLegacyHandlerFilter.FilterHandlersForPhase(allHandlers, phase, @event);

        if (!phaseHandlers.Any()) {
            logEventAction(@event, "ExecutePhase", $"No matching handlers for phase {phase}");
            return LSLegacyPhaseExecutionResult.Successful();
        }

        return executeHandlersInPhase(@event, phase, phaseHandlers);
    }

    private LSLegacyPhaseExecutionResult executeHandlersInPhase<TEvent>(TEvent @event, LSLegacyEventPhase phase, IEnumerable<LSLegacyHandlerRegistration> phaseHandlers) where TEvent : ILSEvent {
        var mutableEvent = (ILSLegacyMutableEvent)@event;
        logEventAction(@event, "ExecuteHandlersInPhase", $"Executing {phaseHandlers.Count()} handlers for phase {phase}");

        // Execute all handlers for this phase
        var startTime = System.DateTime.UtcNow;
        var errors = new List<string>();
        bool hasWaitingHandler = false;

        foreach (var handler in phaseHandlers) {
            var handlerResult = LSLegacyHandlerExecutor.ExecuteHandler(@event, handler, phase, startTime, errors, logEventAction);

            // Handle different handler results
            switch (handlerResult) {
                case LSLegacyPhaseExecutionResult { ShouldWait: true }:
                    logEventAction(@event, "ExecuteHandlersInPhase", $"Handler requested waiting in phase {phase}");
                    mutableEvent.IsWaiting = true;
                    hasWaitingHandler = true;
                    break;

                case LSLegacyPhaseExecutionResult { ShouldCancel: true }:
                    logEventAction(@event, "ExecuteHandlersInPhase", $"Handler requested cancellation in phase {phase}");
                    mutableEvent.IsCancelled = true;
                    if (!string.IsNullOrEmpty(handlerResult.ErrorMessage) && @event is LSLegacyBaseEvent baseEvent) {
                        baseEvent.SetErrorMessage(handlerResult.ErrorMessage);
                    }
                    // Continue with other handlers in this phase, then proceed to CANCEL/COMPLETE phases
                    break;

                case LSLegacyPhaseExecutionResult { HasFailures: true }:
                    logEventAction(@event, "ExecuteHandlersInPhase", $"Handler reported failure in phase {phase}");
                    // Continue with other handlers but track the failure
                    mutableEvent.HasFailures = true;
                    break;

                case LSLegacyPhaseExecutionResult { Success: false }:
                    logEventAction(@event, "ExecuteHandlersInPhase", $"Handler failed in phase {phase}");
                    return handlerResult;
            }
        }

        return processPhaseCompletion(@event, phase, hasWaitingHandler, errors);
    }

    private LSLegacyPhaseExecutionResult processPhaseCompletion<TEvent>(TEvent @event, LSLegacyEventPhase phase, bool hasWaitingHandler, List<string> errors) where TEvent : ILSEvent {
        var mutableEvent = (ILSLegacyMutableEvent)@event;

        // After all handlers have executed, check for deferred resumption
        if (hasWaitingHandler) {
            var deferredResult = processDeferredResumption(@event);
            if (deferredResult.HasValue) {
                logEventAction(@event, "ProcessPhaseCompletion", $"Deferred resumption processed: {deferredResult.Value.ShouldCancel}, {deferredResult.Value.HasFailures}");

                // Apply the deferred state changes to the event
                if (deferredResult.Value.ShouldCancel) {
                    mutableEvent.IsCancelled = true;
                }
                if (deferredResult.Value.HasFailures) {
                    mutableEvent.HasFailures = true;
                }

                // Return successful completion of this phase - the lifecycle will handle the state transitions
                return LSLegacyPhaseExecutionResult.Successful();
            } else {
                // Still waiting for async operation
                return LSLegacyPhaseExecutionResult.Waiting();
            }
        }

        if (errors.Any()) {
            logEventAction(@event, "ProcessPhaseCompletion", $"Phase {phase} completed with errors", new { errorCount = errors.Count, errors });
            return LSLegacyPhaseExecutionResult.Failed($"Phase {phase} had {errors.Count} errors");
        }

        logEventAction(@event, "ProcessPhaseCompletion", $"Phase {phase} completed successfully");
        return LSLegacyPhaseExecutionResult.Successful();
    }

    #endregion

    #region Handler Management

    /// <summary>
    /// Checks if a handler's instance restriction matches the current event.
    /// This is used to filter handlers that are registered for specific instances.
    /// </summary>
    /// <typeparam name="TEvent">The type of event being processed.</typeparam>
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
        LSLegacyEventPhase phase,
        LSPriority priority,
        System.Type? instanceType,
        object? instance,
        int maxExecutions,
        LSLegacyEventCondition<TEvent>? condition
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
        LSLegacyPhaseHandler<TEvent> handlerToRemove,
        LSLegacyEventPhase phase,
        LSPriority priority,
        System.Type? instanceType,
        object? instance,
        int maxExecutions,
        LSLegacyEventCondition<TEvent>? condition
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
    /// Registers a batch of event-scoped handlers as a single unit for optimal performance and integration.
    /// This method is used internally by the LSEventCallbackBuilder system to register event-specific handlers
    /// that execute alongside global handlers.
    /// 
    /// Key integration features:
    /// - Event-scoped handlers only execute for their target event instance
    /// - Global and event-scoped handlers execute together in priority order
    /// - Automatic cleanup after one-time execution (MaxExecutions = 1)
    /// - Proper phase sequencing and async operation support
    /// - Batch optimization for multiple handlers in the same phase
    /// </summary>
    /// <typeparam name="TEvent">The event type to register handlers for.</typeparam>
    /// <param name="builder">The callback builder containing the handlers to register.</param>
    /// <returns>A unique identifier for the registered batch that can be used for unregistration.</returns>
    internal System.Guid registerBatchedHandlers<TEvent>(LSLegacyEventCallbackBuilder<TEvent> builder) where TEvent : ILSEvent {
        if (builder == null) {
            throw new ArgumentNullException(nameof(builder), "Builder cannot be null");
        }

        if (builder.HandlerCount == 0) {
            // Empty builder - still generate an ID but don't register anything
            return System.Guid.NewGuid();
        }

        // Ensure builder is finalized for optimal execution
        if (!builder.IsFinalized) {
            builder.finalizeBatch();
        }

        var batchId = System.Guid.NewGuid();

        // Only register for phases that actually have handlers in the builder
        var phasesWithHandlers = new[] {
            LSLegacyEventPhase.VALIDATE,
            LSLegacyEventPhase.PREPARE,
            LSLegacyEventPhase.EXECUTE,
            LSLegacyEventPhase.SUCCESS,
            LSLegacyEventPhase.FAILURE,
            LSLegacyEventPhase.CANCEL,
            LSLegacyEventPhase.COMPLETE
        }.Where(phase => HasHandlersForPhase(builder, phase)).ToArray();

        lock (_lock) {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) {
                list = new List<LSLegacyHandlerRegistration>();
                _handlers[typeof(TEvent)] = list;
            }

            foreach (var phase in phasesWithHandlers) {
                // Determine the best priority for this phase's batch handler
                var phaseHandlers = GetHandlersForPhase(builder, phase);
                var batchPriority = phaseHandlers.Any()
                    ? phaseHandlers.Min(h => h.Priority)
                    : LSPriority.NORMAL;

                var batchedHandler = new LSLegacyHandlerRegistration {
                    Id = System.Guid.NewGuid(), // Each phase handler gets its own ID
                    EventType = typeof(TEvent),
                    Handler = (evt, ctx) => executeBatchedHandlers((TEvent)evt, ctx, builder),
                    Phase = phase,
                    Priority = batchPriority, // Use the highest priority (lowest enum value) from the batch
                    Instance = null, // Don't use Instance field for event-scoped handlers
                    InstanceType = typeof(TEvent),
                    MaxExecutions = 1, // One-time execution
                    Condition = null, // Don't restrict - let the batched handler internal logic handle targeting
                    ExecutionCount = 0,
                    // Add metadata to identify this as a batched handler
                    BatchId = batchId,
                    TargetEvent = builder.TargetEvent
                };

                list.Add(batchedHandler);
            }

            // Sort by priority to ensure correct execution order
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        return batchId;
    }

    /// <summary>
    /// Helper method to check if a builder has handlers for a specific phase.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="builder">The builder to check.</param>
    /// <param name="phase">The phase to check for.</param>
    /// <returns>True if the builder has handlers for the specified phase.</returns>
    private bool HasHandlersForPhase<TEvent>(LSLegacyEventCallbackBuilder<TEvent> builder, LSLegacyEventPhase phase) where TEvent : ILSEvent {
        return builder.Handlers.Any(h => h.Phase == phase);
    }

    /// <summary>
    /// Helper method to get handlers for a specific phase from a builder.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="builder">The builder to get handlers from.</param>
    /// <param name="phase">The phase to get handlers for.</param>
    /// <returns>Handlers for the specified phase.</returns>
    private IEnumerable<LSLegacyBatchedHandler<TEvent>> GetHandlersForPhase<TEvent>(LSLegacyEventCallbackBuilder<TEvent> builder, LSLegacyEventPhase phase) where TEvent : ILSEvent {
        return builder.Handlers.Where(h => h.Phase == phase).OrderBy(h => h.Priority);
    }

    /// <summary>
    /// Executes batched handlers for the current phase.
    /// </summary>
    /// <typeparam name="TEvent">The event type being processed.</typeparam>
    /// <param name="evt">The event instance being processed.</param>
    /// <param name="ctx">The current phase context.</param>
    /// <param name="builder">The builder containing the handlers to execute.</param>
    /// <returns>The result of the batch execution.</returns>
    private LSHandlerResult executeBatchedHandlers<TEvent>(TEvent evt, LSLegacyPhaseContext ctx, LSLegacyEventCallbackBuilder<TEvent> builder)
        where TEvent : ILSEvent {

        // Only execute if this is the target event for this builder
        if (!ReferenceEquals(evt, builder.TargetEvent)) {
            return LSHandlerResult.CONTINUE; // Skip silently for other events
        }

        var result = LSHandlerResult.CONTINUE;

        // Execute only handlers for the current phase
        var phaseHandlers = GetHandlersForPhase(builder, ctx.CurrentPhase);

        foreach (var handler in phaseHandlers) {
            try {
                // Check if event was cancelled (e.g., via immediate Abort() call)
                // BUT allow CANCEL and COMPLETE phase handlers to run even when event is cancelled
                if (evt.IsCancelled && ctx.CurrentPhase != LSLegacyEventPhase.CANCEL && ctx.CurrentPhase != LSLegacyEventPhase.COMPLETE) {
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
                            var mutableEvent = (ILSLegacyMutableEvent)evt;
                            mutableEvent.IsWaiting = true;

                            // Check if async operation already completed (scenario 2: immediate completion)
                            if (evt is LSLegacyBaseEvent baseEvent) {
                                var deferredResumption = baseEvent.GetDeferredResumption();
                                if (deferredResumption.HasValue) {
                                    // Async operation completed before we set IsWaiting = true
                                    baseEvent.ClearDeferredResumption();
                                    mutableEvent.IsWaiting = false;

                                    // Process the deferred action
                                    switch (deferredResumption.Value) {
                                        case LSLegacyBaseEvent.ResumptionType.Resume:
                                            continue; // Continue with next handler
                                        case LSLegacyBaseEvent.ResumptionType.Abort:
                                            return LSHandlerResult.CANCEL; // Cancel entire event processing
                                        case LSLegacyBaseEvent.ResumptionType.Fail:
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
                            var mutableEvt = (ILSLegacyMutableEvent)evt;
                            mutableEvt.HasFailures = true;
                            if (evt is LSLegacyBaseEvent failureEvent) {
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
                if (ctx.CurrentPhase == LSLegacyEventPhase.VALIDATE && handler.Priority == LSPriority.CRITICAL) {
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
