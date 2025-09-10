using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

public class EventSystemRegister<TEvent> where TEvent : ILSEvent {
    private readonly LSESDispatcher _dispatcher;
    protected List<IHandlerEntry> _entries = new();

    internal EventSystemRegister(LSESDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }
    public EventSystemRegister<TEvent> OnState<TState>(Func<StateHandlerRegister<TState>, StateHandlerEntry> configureStateHandler) where TState : IEventSystemState {
        var register = new StateHandlerRegister<TState>(_dispatcher);
        var entry = configureStateHandler(register);
        if (entry == null) throw new LSArgumentNullException(nameof(entry));
        _entries.Add(entry);
        return this;
    }
    public EventSystemRegister<TEvent> OnPhase<TPhase>(Func<PhaseHandlerRegister<TPhase>, PhaseHandlerEntry> configurePhaseHandler) where TPhase : BusinessState.PhaseState {
        var register = PhaseHandlerRegister<TPhase>.Create(_dispatcher);
        var entry = configurePhaseHandler(register);
        if (entry == null) throw new LSArgumentNullException(nameof(entry));
        _entries.Add(entry);
        return this;
    }
    public System.Guid[] Register() {
        var ids = new List<System.Guid>();
        foreach (var entry in _entries) {
            var id = _dispatcher.registerHandler(typeof(TEvent), entry);
            ids.Add(id);
        }
        return ids.ToArray();
    }

}
