using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Succeed state implementation for v4.
/// Handles post-success processing and transitions to completed state.
/// </summary>
public class SucceedState : IEventSystemState {
    protected readonly EventSystemContext _context;

    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    public bool HasFailures => false;
    public bool HasCancelled => false;
    protected Stack<StateHandlerEntry> _handlers = new();

    public SucceedState(EventSystemContext context) {
        _context = context;
        var handlers = _context.Handlers.OfType<StateHandlerEntry>().OrderByDescending(h => h.Priority).ToList();
        foreach (var handler in handlers) _handlers.Push(handler);
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
