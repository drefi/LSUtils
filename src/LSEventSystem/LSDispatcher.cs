using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Clean, phase-based event dispatcher that coordinates the execution of event handlers
/// across different phases. This is the main component of the LSUtils V2 event system
/// and provides a predictable, organized approach to event processing.
/// 
/// The dispatcher processes events through five distinct phases:
/// VALIDATE -> PREPARE -> EXECUTE -> FINALIZE -> COMPLETE
/// 
/// Each phase serves a specific purpose and handlers are executed in priority order
/// within each phase. This provides clear separation of concerns and predictable execution flow.
/// 
/// Key Features:
/// - Thread-safe handler registration and execution
/// - Conditional handler execution with instance filtering
/// - Automatic handler cleanup and lifecycle management
/// - Priority-based execution within phases
/// - Comprehensive error handling and retry mechanisms
/// - Integration with event-scoped callback builders
/// - Runtime handler inspection and monitoring capabilities
/// </summary>
public class LSDispatcher {
    public static LSDispatcher Singleton { get; } = new LSDispatcher();
    public LSDispatcher() { }
    private readonly ConcurrentDictionary<System.Type, List<LSHandlerRegistration>> _handlers = new();
    private readonly object _lock = new object();

    /// <summary>
    /// Creates a fluent registration builder for the specified event type.
    /// This is the recommended way to register handlers with complex configurations.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register handlers for.</typeparam>
    /// <returns>A registration builder that allows fluent configuration of the handler.</returns>
    /// <example>
    /// <code>
    /// dispatcher.Build&lt;MyEvent&gt;()
    ///     .InPhase(LSEventPhase.VALIDATE)
    ///     .WithPriority(LSPhasePriority.HIGH)
    ///     .ForInstance(myInstance)
    ///     .Register(myHandler);
    /// </code>
    /// </example>
    public LSEventRegistration<TEvent> Build<TEvent>() where TEvent : ILSEvent {
        return new LSEventRegistration<TEvent>(this);
    }

     /// <summary>
    /// Full registration method with all available options. This is used internally
    /// by the fluent registration API but can also be called directly for maximum control.
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
    internal System.Guid RegisterHandler<TEvent>(
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

    /// <summary>
    /// Processes an event through all phases in the correct order.
    /// The event will go through VALIDATE, PREPARE, EXECUTE, FINALIZE, CANCEL (if cancelled), and COMPLETE phases.
    /// If the event is aborted in any phase (except COMPLETE), the CANCEL phase will run followed by the COMPLETE phase for cleanup.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to process.</typeparam>
    /// <param name="event">The event instance to process.</param>
    /// <returns>True if the event completed successfully, false if it was aborted.</returns>
    public bool ProcessEvent<TEvent>(TEvent @event) where TEvent : ILSEvent {
        return ProcessEventInternal(@event, resuming: false);
    }

    /// <summary>
    /// Resumes processing of an event that was paused in a WAITING state.
    /// This method should be called after the async operation completes and
    /// Resume() has been called on the event.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to resume processing for.</typeparam>
    /// <param name="event">The event instance to resume processing.</param>
    /// <returns>True if the event completed successfully, false if it was aborted.</returns>
    internal bool ContinueProcessing<TEvent>(TEvent @event) where TEvent : ILSEvent {
        return ProcessEventInternal(@event, resuming: true);
    }

    /// <summary>
    /// Internal method that handles both initial processing and resumption from WAITING state.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to process.</typeparam>
    /// <param name="event">The event instance to process.</param>
    /// <param name="resuming">True if resuming from WAITING state, false for initial processing.</param>
    /// <returns>True if the event completed successfully, false if it was aborted.</returns>
    private bool ProcessEventInternal<TEvent>(TEvent @event, bool resuming) where TEvent : ILSEvent {
        if (@event.IsCompleted || (@event.IsCancelled && !resuming)) {
            return @event.IsCompleted;
        }

        var mutableEvent = (ILSMutableEvent)@event;
        
        if (resuming && mutableEvent.IsWaiting) {
            // Event is still waiting, cannot resume yet
            return false;
        }

        var startTime = System.DateTime.UtcNow;
        var errors = new List<string>();

        var phases = new[] {
            LSEventPhase.VALIDATE,
            LSEventPhase.PREPARE,
            LSEventPhase.EXECUTE,
            LSEventPhase.SUCCESS,    // Only runs when not cancelled
            LSEventPhase.CANCEL,    // Only runs when cancelled
            LSEventPhase.COMPLETE   // Always runs
        };

        // Determine starting phase
        int startPhaseIndex = 0;
        if (resuming) {
            startPhaseIndex = System.Array.IndexOf(phases, mutableEvent.CurrentPhase);
            if (startPhaseIndex == -1) startPhaseIndex = 0;
        }

        for (int i = startPhaseIndex; i < phases.Length; i++) {
            var phase = phases[i];
            
            // Skip SUCCESS phase if event is cancelled
            if (phase == LSEventPhase.SUCCESS && @event.IsCancelled) {
                continue;
            }
            
            // Skip CANCEL phase unless event is cancelled
            if (phase == LSEventPhase.CANCEL && !@event.IsCancelled) {
                continue;
            }
            
            // Skip other phases if cancelled (except SUCCESS, CANCEL and COMPLETE)
            if (@event.IsCancelled && phase != LSEventPhase.SUCCESS && phase != LSEventPhase.CANCEL && phase != LSEventPhase.COMPLETE) {
                continue;
            }

            var phaseResult = ExecutePhase(@event, phase, startTime, errors);
            
            // Check if event is waiting for async operation
            if (mutableEvent.IsWaiting) {
                // Event is paused waiting for async operation
                // The event should call ContinueProcessing() to resume
                return false;
            }

            if (!phaseResult) {
                mutableEvent.IsCancelled = true;
                // Continue to CANCEL phase (if not already there) then COMPLETE
                // The loop will handle this automatically
            }

            mutableEvent.CompletedPhases |= phase;
        }

        mutableEvent.IsCompleted = true;
        return !@event.IsCancelled;
    }

    /// <summary>
    /// Executes all handlers for a specific phase of an event.
    /// </summary>
    /// <typeparam name="TEvent">The type of event being processed.</typeparam>
    /// <param name="event">The event instance being processed.</param>
    /// <param name="phase">The phase to execute.</param>
    /// <param name="startTime">The time when event processing started.</param>
    /// <param name="errors">List to collect error messages.</param>
    /// <returns>True if the phase executed successfully, false if it should abort.</returns>
    private bool ExecutePhase<TEvent>(TEvent @event, LSEventPhase phase, System.DateTime startTime, System.Collections.Generic.List<string> errors) where TEvent : ILSEvent {
        var mutableEvent = (ILSMutableEvent)@event;
        mutableEvent.CurrentPhase = phase;

        var type = @event.GetType();

        if (!_handlers.TryGetValue(type, out var allHandlers)) {
            return true; // No handlers registered for this event type
        }

        // Filter handlers for this phase with all conditions
        var phaseHandlers = allHandlers
            .Where(h => h.Phase == phase)
            .Where(h => h.MaxExecutions == -1 || h.ExecutionCount < h.MaxExecutions)
            .Where(h => MatchesInstance(h, @event))
            .Where(h => h.Condition == null || h.Condition(@event))
            .ToList();

        var handlerCount = 0;

        foreach (var handler in phaseHandlers) {
            try {
                var elapsed = System.DateTime.UtcNow - startTime;
                var context = new LSPhaseContext(phase, handler.Priority, elapsed, handlerCount, errors);

                var result = handler.Handler(@event, context);
                handler.ExecutionCount++;
                handlerCount++;

                switch (result) {
                    case LSPhaseResult.CONTINUE:
                        continue;
                    case LSPhaseResult.SKIP_REMAINING:
                        return true;
                    case LSPhaseResult.CANCEL:
                        // Track which phase the cancellation occurred in
                        if (@event is LSBaseEvent cancelEvent) {
                            cancelEvent.SetData("cancel.phase", phase);
                            cancelEvent.SetData("cancel.time", System.DateTime.UtcNow);
                            cancelEvent.SetData("cancel.handler.id", handler.Id.ToString());
                        }
                        return false;
                    case LSPhaseResult.WAITING:
                        // Handler requested to wait for async operation
                        mutableEvent.IsWaiting = true;
                        
                        // Store resumption state in event data for debugging/monitoring
                        if (@event is LSBaseEvent baseEvent) {
                            baseEvent.SetData("waiting.phase", phase.ToString());
                            baseEvent.SetData("waiting.handler.id", handler.Id.ToString());
                            baseEvent.SetData("waiting.started.at", System.DateTime.UtcNow);
                            baseEvent.SetData("waiting.handler.count", handlerCount);
                        }
                        
                        // Return true to indicate phase should be considered "successful" but paused
                        // The caller will check IsWaiting and handle the pause appropriately
                        return true;
                    case LSPhaseResult.RETRY:
                        // Simple retry logic - could be enhanced with more sophisticated retry policies
                        if (handler.ExecutionCount < 3) {
                            result = handler.Handler(@event, context);
                            if (result == LSPhaseResult.CANCEL) return false;
                        }
                        break;
                }
            } catch (LSException ex) {
                errors.Add($"Handler {handler.Id} in phase {phase} failed: {ex.Message}");
                // Continue with other handlers unless it's a critical validation failure
                if (phase == LSEventPhase.VALIDATE && handler.Priority == LSPhasePriority.CRITICAL) {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if a handler's instance restriction matches the current event.
    /// This is used to filter handlers that are registered for specific instances.
    /// </summary>
    /// <typeparam name="TEvent">The type of event being processed.</typeparam>
    /// <param name="handler">The handler registration to check.</param>
    /// <param name="event">The event being processed.</param>
    /// <returns>True if the handler should execute for this event.</returns>
    private bool MatchesInstance<TEvent>(LSHandlerRegistration handler, TEvent @event) where TEvent : ILSEvent {
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
    internal int UnregisterHandlers<TEvent>(
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
    internal int UnregisterHandler<TEvent>(
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
    internal System.Guid RegisterBatchedHandlers<TEvent>(LSEventCallbackBatch<TEvent> batch) where TEvent : ILSEvent {
        var batchId = System.Guid.NewGuid();
        
        // Register batch handlers for all phases that have handlers
        var phases = new[] {
            LSEventPhase.VALIDATE,
            LSEventPhase.PREPARE,
            LSEventPhase.EXECUTE,
            LSEventPhase.SUCCESS,
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
                        Handler = (evt, ctx) => ExecuteBatchedHandlers((TEvent)evt, ctx, batch),
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
    private LSPhaseResult ExecuteBatchedHandlers<TEvent>(TEvent evt, LSPhaseContext ctx, LSEventCallbackBatch<TEvent> batch) 
        where TEvent : ILSEvent {
        
        var result = LSPhaseResult.CONTINUE;
        
        // Execute only handlers for the current phase
        var phaseHandlers = batch.GetHandlersForPhase(ctx.CurrentPhase);
        
        foreach (var handler in phaseHandlers) {
            try {
                // Check handler condition if present
                if (handler.Condition?.Invoke(evt) ?? true) {
                    var handlerResult = handler.Handler(evt, ctx);
                    
                    // Update result based on handler outcome
                    switch (handlerResult) {
                        case LSPhaseResult.CONTINUE:
                            continue;
                        case LSPhaseResult.SKIP_REMAINING:
                            return handlerResult; // Skip remaining handlers in this batch
                        case LSPhaseResult.CANCEL:
                            return handlerResult; // Cancel entire event processing
                        case LSPhaseResult.WAITING:
                            // Handler requested to wait for async operation
                            var mutableEvent = (ILSMutableEvent)evt;
                            mutableEvent.IsWaiting = true;
                            
                            // Store resumption state in event data for debugging/monitoring
                            if (evt is LSBaseEvent baseEvent) {
                                baseEvent.SetData("waiting.phase", ctx.CurrentPhase.ToString());
                                baseEvent.SetData("waiting.handler.id", System.Guid.NewGuid().ToString()); // Batch handler doesn't have individual ID
                                baseEvent.SetData("waiting.started.at", System.DateTime.UtcNow);
                                baseEvent.SetData("waiting.handler.count", 0); // Within batch
                            }
                            
                            return handlerResult; // Return WAITING to signal pause
                        case LSPhaseResult.RETRY:
                            // For batch handlers, we don't support retry at individual handler level
                            // Convert to continue to avoid infinite loops
                            continue;
                    }
                }
            } catch (LSException ex) {
                // Log error but continue with remaining handlers unless it's critical validation
                if (ctx.CurrentPhase == LSEventPhase.VALIDATE && handler.Priority == LSPhasePriority.CRITICAL) {
                    evt.SetData("batch.error", ex.Message);
                    return LSPhaseResult.CANCEL;
                }
                
                // For non-critical errors, log and continue
                evt.SetData($"batch.error.{ctx.CurrentPhase}.{handler.Priority}", ex.Message);
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
}
