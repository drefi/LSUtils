using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Context for state transitions and phase execution in v4.
/// Provides state management interface for the event processing pipeline.
/// </summary>
public class EventSystemContext {
    public LSESDispatcher Dispatcher { get; }
    public IEventSystemState CurrentState { get; internal set; }
    public ILSEvent Event { get; internal set; }
    public IReadOnlyList<IHandlerEntry> Handlers { get; }

    /// <summary>
    /// Indicates if the processing failed but can continue.
    /// </summary>
    public bool HasFailures { get; internal set; }
    public bool IsCancelled { get; internal set; }

    public EventSystemContext(LSESDispatcher dispatcher, ILSEvent @event, IReadOnlyList<IHandlerEntry> handlers) {
        Event = @event;
        Dispatcher = dispatcher;
        CurrentState = new BusinessState(this);
        Handlers = handlers;
    }

    internal StateProcessResult processEvent() {
        do {
            var nextState = CurrentState.Process();
            var result = CurrentState.StateResult;
            switch (result) {
                case StateProcessResult.CANCELLED:
                    IsCancelled = true;
                    return StateProcessResult.CANCELLED;
                case StateProcessResult.FAILURE:
                    HasFailures = true;
                    break;
                case StateProcessResult.WAITING:
                    //stay in current state
                    return StateProcessResult.WAITING;
                case StateProcessResult.CONTINUE:
                default:
                    break;
            }

        } while (CurrentState != null);
        return HasFailures ? StateProcessResult.FAILURE : StateProcessResult.CONTINUE;
    }

    /// <summary>
    /// Handles waiting state resumption.
    /// </summary>
    public IEventSystemState? Resume() => CurrentState.Resume();

    /// <summary>
    /// Handles waiting state abortion.
    /// </summary>
    public IEventSystemState? Cancel() => CurrentState.Cancel();
    public IEventSystemState? Fail() => CurrentState.Fail();

    internal bool getHandlersForState(System.Type stateType, out Stack<StateHandlerEntry> handlersStack) {
        handlersStack = new Stack<StateHandlerEntry>();
        var handlers = Handlers.OfType<StateHandlerEntry>()
            .Where(h => h.StateType == stateType)
            .OrderByDescending(h => h.Priority);
        if (!handlers.Any()) return false;
        foreach (var handler in handlers) handlersStack.Push(handler);
        return true;
    }
    internal bool getHandlersForPhase(EventSystemPhase phase, out Stack<PhaseHandlerEntry> handlersStack) {
        handlersStack = new Stack<PhaseHandlerEntry>();
        var handlers = Handlers.OfType<PhaseHandlerEntry>()
            .Where(h => h.Phase == phase)
            .OrderByDescending(h => h.Priority);
        if (!handlers.Any()) return false;
        foreach (var handler in handlers) handlersStack.Push(handler);
        return true;
    }
}
