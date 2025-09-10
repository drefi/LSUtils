using System.Collections.Generic;

namespace LSUtils.EventSystem;

public class CompletedState : IEventSystemState {
    protected readonly EventSystemContext _context;
    protected Stack<StateHandlerEntry> _handlers = new();
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    public bool HasFailures => false;
    public bool HasCancelled => false;

    public CompletedState(EventSystemContext context) {
        _context = context;
    }

    public IEventSystemState? Process() {
        while (_handlers.Count > 0) {
            var handlerEntry = _handlers.Pop();
            if (!handlerEntry.Condition(_context.Event, handlerEntry)) continue;
            handlerEntry.Handler(_context.Event);
        }

        StateResult = StateProcessResult.SUCCESS;
        return null;
    }

    public IEventSystemState? Resume() {
        return null;
    }

    public IEventSystemState? Cancel() {
        return null;
    }

    public IEventSystemState? Fail() {
        return null;
    }
}
