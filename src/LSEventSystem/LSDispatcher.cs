using System;
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
/// </summary>
public class LSDispatcher {
    public static LSDispatcher Singleton { get; } = new LSDispatcher();
    public LSDispatcher() { }
    private readonly ConcurrentDictionary<Type, List<LSHandlerRegistration>> _handlers = new();
    private readonly object _lock = new object();

    /// <summary>
    /// Creates a fluent registration builder for the specified event type.
    /// This is the recommended way to register handlers with complex configurations.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register handlers for.</typeparam>
    /// <returns>A registration builder that allows fluent configuration of the handler.</returns>
    /// <example>
    /// <code>
    /// dispatcher.For&lt;MyEvent&gt;()
    ///     .InPhase(LSEventPhase.VALIDATE)
    ///     .WithPriority(LSPhasePriority.HIGH)
    ///     .ForInstance(myInstance)
    ///     .Register(myHandler);
    /// </code>
    /// </example>
    public LSEventRegistration<TEvent> For<TEvent>() where TEvent : ILSEvent {
        return new LSEventRegistration<TEvent>(this);
    }

    /// <summary>
    /// Simple registration method for common cases where only basic configuration is needed.
    /// For more complex scenarios, use the fluent For&lt;TEvent&gt;() API.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register the handler for.</typeparam>
    /// <param name="handler">The handler function to execute.</param>
    /// <param name="phase">The phase in which to execute the handler (default: EXECUTE).</param>
    /// <param name="priority">The execution priority within the phase (default: NORMAL).</param>
    /// <returns>A unique identifier for the registered handler.</returns>
    public Guid RegisterHandler<TEvent>(
        LSPhaseHandler<TEvent> handler,
        LSEventPhase phase = LSEventPhase.EXECUTE,
        LSPhasePriority priority = LSPhasePriority.NORMAL
    ) where TEvent : ILSEvent {
        return RegisterHandler(handler, phase, priority, null, null, -1, null);
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
    internal Guid RegisterHandler<TEvent>(
        LSPhaseHandler<TEvent> handler,
        LSEventPhase phase,
        LSPhasePriority priority,
        Type? instanceType,
        object? instance,
        int maxExecutions,
        Func<TEvent, bool>? condition
    ) where TEvent : ILSEvent {
        var registration = new LSHandlerRegistration {
            Id = Guid.NewGuid(),
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
    /// The event will go through VALIDATE, PREPARE, EXECUTE, FINALIZE, and COMPLETE phases.
    /// If the event is aborted in any phase (except COMPLETE), only the COMPLETE phase will run for cleanup.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to process.</typeparam>
    /// <param name="event">The event instance to process.</param>
    /// <returns>True if the event completed successfully, false if it was aborted.</returns>
    public bool ProcessEvent<TEvent>(TEvent @event) where TEvent : ILSEvent {
        if (@event.IsCompleted || @event.IsCancelled) {
            return false;
        }

        var phases = new[] {
            LSEventPhase.VALIDATE,
            LSEventPhase.PREPARE,
            LSEventPhase.EXECUTE,
            LSEventPhase.FINALIZE,
            LSEventPhase.COMPLETE
        };

        var startTime = DateTime.UtcNow;
        var errors = new List<string>();
        var mutableEvent = (ILSMutableEvent)@event;

        foreach (var phase in phases) {
            // Skip phases if cancelled, except COMPLETE which always runs for cleanup
            if (@event.IsCancelled && phase != LSEventPhase.COMPLETE) {
                continue;
            }

            if (!ExecutePhase(@event, phase, startTime, errors)) {
                mutableEvent.IsCancelled = true;
                // Ensure COMPLETE phase runs for cleanup if we're not already in it
                if (phase != LSEventPhase.COMPLETE) {
                    ExecutePhase(@event, LSEventPhase.COMPLETE, startTime, errors);
                }
                return false;
            }

            mutableEvent.CompletedPhases |= phase;
        }

        mutableEvent.IsCompleted = true;
        return true;
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
    private bool ExecutePhase<TEvent>(TEvent @event, LSEventPhase phase, DateTime startTime, List<string> errors) where TEvent : ILSEvent {
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
                var elapsed = DateTime.UtcNow - startTime;
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
                        return false;
                    case LSPhaseResult.RETRY:
                        // Simple retry logic - could be enhanced with more sophisticated retry policies
                        if (handler.ExecutionCount < 3) {
                            result = handler.Handler(@event, context);
                            if (result == LSPhaseResult.CANCEL) return false;
                        }
                        break;
                }
            } catch (Exception ex) {
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
    public bool UnregisterHandler(Guid handlerId) {
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
