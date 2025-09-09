using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Clean v4 dispatcher that acts as a state manager for event processing.
/// Implements proper state pattern with single public method for global handler registration.
/// </summary>
public class LSESDispatcher {
    /// <summary>
    /// Gets the singleton instance of the dispatcher.
    /// </summary>
    public static LSESDispatcher Singleton { get; } = new LSESDispatcher();

    private readonly Dictionary<Type, List<IHandlerEntry>> _handlers = new();

    /// <summary>
    /// The only public method for registering global handlers.
    /// Clean API that fundamentally changes how handlers are registered.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register handlers for.</typeparam>
    /// <param name="configurePhaseHandler">Action to configure the event registration.</param>
    /// <returns>True if the handler was successfully registered.</returns>
    public bool ForEvent<TEvent>(LSAction<PhaseHandlerRegister<TEvent>> configurePhaseHandler) where TEvent : ILSEvent {
        try {
            var register = new PhaseHandlerRegister<TEvent>(this);
            configurePhaseHandler(register);
            return true;
        } catch {
            return false;
        }
    }
    public bool ForEvent<TEvent>(LSAction<StateHandlerRegister<TEvent>> configureStateHandler) where TEvent : ILSEvent {
        try {
            var register = new StateHandlerRegister<TEvent>(this);
            configureStateHandler(register);
            return true;
        } catch {
            return false;
        }
    }
    /// <summary>
    /// Internal method to register a handler entry.
    /// </summary>
    internal System.Guid registerHandler(Type eventType, IHandlerEntry entry) {
        if (!_handlers.ContainsKey(eventType)) {
            _handlers[eventType] = new List<IHandlerEntry>();
        }
        _handlers[eventType].Add(entry);
        return entry.ID;
    }

    /// <summary>
    /// Internal method to get handlers for a specific phase.
    /// Used by state implementations for phase processing.
    /// </summary>
    // internal List<PhaseHandlerEntry> getHandlersForPhase(Type eventType, EventSystemPhases phase) {
    //     if (!_handlers.TryGetValue(eventType, out var handlers)) {
    //         return new List<PhaseHandlerEntry>();
    //     }

    //     return handlers
    //         .OfType<PhaseHandlerEntry>()
    //         .Where(h => h.Phase == phase)
    //         .ToList();
    // }
    // internal List<StateHandlerEntry> getHandlersForState(Type eventType, Type stateType) {
    //     if (!_handlers.TryGetValue(eventType, out var handlers)) {
    //         return new List<StateHandlerEntry>();
    //     }

    //     return handlers
    //         .OfType<StateHandlerEntry>()
    //         .Where(h => h.StateType == stateType)
    //         .ToList();
    // }
    internal List<IHandlerEntry> getHandlers(Type eventType) {
        return _handlers.TryGetValue(eventType, out var handlers) ? handlers : new List<IHandlerEntry>();
    }

    /// <summary>
    /// Internal method to process an event through the state machine.
    /// Creates and manages the state context for the event.
    /// </summary>
    internal StateProcessResult processEvent(ILSEvent @event, IReadOnlyList<IHandlerEntry> handlers) {
        var context = new EventSystemContext(this, @event, handlers);
        return context.processEvent();
    }
}
