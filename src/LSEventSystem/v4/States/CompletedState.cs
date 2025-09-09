using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Completed state implementation for v4.
/// Final state - no further processing possible.
/// </summary>
public class CompletedState : IEventSystemState {
    protected readonly EventSystemContext _context;
    protected Stack<StateHandlerEntry> _handlers = new();
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;

    public CompletedState(EventSystemContext context) {
        _context = context;
    }

    public IEventSystemState? Process() {
        while (_handlers.Count > 0) {
            var handlerEntry = _handlers.Pop();
            if (!handlerEntry.Condition(_context.Event, handlerEntry)) continue;
            handlerEntry.Handler(_context.Event);
        }

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
