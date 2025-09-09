using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Phase registration builder.
/// </summary>
public class PhaseHandlerRegister<TEvent> where TEvent : ILSEvent {
    protected readonly LSESDispatcher _dispatcher;
    protected EventSystemPhase _phase = EventSystemPhase.EXECUTE;
    protected Func<EventSystemContext, HandlerProcessResult>? _handler = null;
    protected LSESPriority _priority = LSESPriority.NORMAL;
    protected Func<ILSEvent, IHandlerEntry, bool> _condition = (evt, entry) => true;

    internal PhaseHandlerRegister(LSESDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Registers a conditional handler for the specified phase.
    /// </summary>
    public PhaseHandlerRegister<TEvent> OnPhase(EventSystemPhase phase) {
        _phase = phase;
        return this;
    }


    public PhaseHandlerRegister<TEvent> WithPriority(LSESPriority priority) {
        _priority = priority;
        return this;
    }
    public PhaseHandlerRegister<TEvent> When(Func<ILSEvent, IHandlerEntry, bool> condition) {
        if (condition == null) throw new LSArgumentNullException(nameof(condition));
        _condition += condition;
        return this;
    }

    public System.Guid Register(Func<EventSystemContext, HandlerProcessResult> handler) {
        var entry = new PhaseHandlerEntry {
            Phase = _phase,
            Priority = _priority,
            Handler = handler,
            Condition = _condition
        };

        return _dispatcher.registerHandler(typeof(TEvent), entry);
    }

    /// <summary>
    /// Registers different handlers for multiple phases using a dictionary.
    /// </summary>
    public void Batch(EventSystemPhase phase, params Func<EventSystemContext, HandlerProcessResult>[] handlers) {
        foreach (var handler in handlers) {
            var entry = new PhaseHandlerEntry {
                Phase = phase,
                Priority = _priority,
                Handler = handler,
                Condition = _condition
            };
            _dispatcher.registerHandler(typeof(TEvent), entry);
        }
    }
}
