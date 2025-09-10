using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Cancelled state implementation for v4.
/// Terminal state for cancelled events.
/// </summary>
public class CancelledState : IEventSystemState {
    protected readonly EventSystemContext _context;
    protected Stack<StateHandlerEntry> _handlers = new();
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    public bool HasFailures => false;
    public bool HasCancelled => true; //always true for CancelledState since the StateResult is different from actually being cancelled

    public CancelledState(EventSystemContext context) {
        _context = context;
    }

    public IEventSystemState? Process() {
        while (_handlers.Count > 0) {
            var handlerEntry = _handlers.Pop();
            if (!handlerEntry.Condition(_context.Event, handlerEntry)) continue;
            handlerEntry.Handler(_context.Event);
        }

        StateResult = StateProcessResult.SUCCESS;
        return new CompletedState(_context);
    }
    public IEventSystemState? Resume() => null;
    public IEventSystemState? Cancel() => null;
    public IEventSystemState? Fail() => null;
}
