using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

public class LSESDispatcher {
    public static LSESDispatcher Singleton { get; } = new LSESDispatcher();

    private readonly Dictionary<Type, List<IHandlerEntry>> _handlers = new();

    public System.Guid[] ForEvent<TEvent>(Func<EventSystemRegister<TEvent>, System.Guid[]> configureRegister) where TEvent : ILSEvent {
        try {
            var register = new EventSystemRegister<TEvent>(this);
            return configureRegister(register);
        } catch {
            return Array.Empty<System.Guid>();
        }
    }
    // used to register phase handlers
    public System.Guid ForEventPhase<TEvent, TPhase>(Func<PhaseHandlerRegister<TPhase>, PhaseHandlerEntry> configurePhaseHandler) where TEvent : ILSEvent where TPhase : BusinessState.PhaseState {
        try {
            var register = PhaseHandlerRegister<TPhase>.Create(this);
            var entry = configurePhaseHandler(register);
            if (entry == null) throw new LSArgumentNullException(nameof(entry));
            return registerHandler(typeof(TEvent), entry);
        } catch {
            return System.Guid.Empty;
        }
    }
    // used to register state handlers
    public System.Guid ForEventState<TEvent, TState>(Func<StateHandlerRegister<TState>, StateHandlerEntry> configureStateHandler) where TEvent : ILSEvent where TState : IEventSystemState {
        try {
            var register = new StateHandlerRegister<TState>(this);
            var entry = configureStateHandler(register);
            if (entry == null) throw new LSArgumentNullException(nameof(entry));
            return registerHandler(typeof(TEvent), entry);
        } catch {
            return System.Guid.Empty;
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
    /// Internal method to get handlers for a specific event type.
    /// Can only be called by the event itself.
    /// </summary>
    internal List<IHandlerEntry> getHandlers(Type eventType) {
        return _handlers.TryGetValue(eventType, out var handlers) ? handlers : new List<IHandlerEntry>();
    }

}
