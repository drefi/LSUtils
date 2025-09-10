using System;
using System.Collections.Generic;
namespace LSUtils.EventSystem;

public class PhaseHandlerRegister<TPhase> where TPhase : BusinessState.PhaseState {
    protected readonly LSESDispatcher _dispatcher;
    //protected EventSystemPhase _phase = EventSystemPhase.EXECUTE;
    protected System.Type _phaseType = typeof(TPhase);
    protected Func<EventSystemContext, HandlerProcessResult>? _handler = null;
    protected LSESPriority _priority = LSESPriority.NORMAL;
    protected Func<ILSEvent, IHandlerEntry, bool> _condition = (evt, entry) => true;
    public bool IsBuild { get; protected set; } = false;
    protected PhaseHandlerEntry? _entry = null;

    protected PhaseHandlerRegister(LSESDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }
    // public PhaseHandlerRegister<TPhase> OnPhase(EventSystemPhase phase) {
    //     _phase = phase;
    //     return this;
    // }
    public PhaseHandlerRegister<TPhase> WithPriority(LSESPriority priority) {
        _priority = priority;
        return this;
    }
    public PhaseHandlerRegister<TPhase> When(Func<ILSEvent, IHandlerEntry, bool> condition) {
        if (condition == null) throw new LSArgumentNullException(nameof(condition));
        _condition += condition;
        return this;
    }
    public PhaseHandlerRegister<TPhase> Handler(Func<EventSystemContext, HandlerProcessResult> handler) {
        _handler = handler;
        return this;
    }
    public PhaseHandlerEntry Build() {
        if (_handler == null) throw new LSArgumentNullException(nameof(_handler));
        if (_phaseType == null) throw new LSException("invalid_phase_none");
        if (IsBuild && _entry != null) return _entry;
        _entry = new PhaseHandlerEntry {
            //Phase = _phase,
            PhaseType = _phaseType,
            Priority = _priority,
            Handler = _handler,
            Condition = _condition
        };
        IsBuild = true;
        return _entry;
    }
    public System.Guid Register() {
        if (IsBuild) throw new LSException("handler_already_built");
        var entry = Build();
        return _dispatcher.registerHandler(typeof(TPhase), entry);
    }

    internal static PhaseHandlerRegister<TPhase> Create(LSESDispatcher dispatcher) {
        return new PhaseHandlerRegister<TPhase>(dispatcher);
    }
}
