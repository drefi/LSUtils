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
    public IEventSystemState? CurrentState { get; internal set; }
    public ILSEvent Event { get; internal set; }
    public IReadOnlyList<IHandlerEntry> Handlers { get; }

    /// <summary>
    /// Indicates if the processing failed but can continue.
    /// </summary>
    public bool HasFailures { get; internal set; }
    public bool IsCancelled { get; internal set; }

    protected EventSystemContext(LSESDispatcher dispatcher, ILSEvent @event, IReadOnlyList<IHandlerEntry> handlers) {
        Event = @event;
        Dispatcher = dispatcher;
        Handlers = handlers;
        CurrentState = new BusinessState(this);
    }

    /// <summary>
    /// Processes the event through its lifecycle until completion, cancellation, or waiting state.
    /// Can only be called by the event itself.
    /// </summary>
    internal EventProcessResult processEvent() {
        if (Event.InDispatch == false) throw new LSException("Event must be marked as InDispatch before processing.");
        while (CurrentState != null) {
            var nextState = CurrentState.Process();
            var result = CurrentState.StateResult;
            switch (result) {
                case StateProcessResult.CANCELLED:
                    IsCancelled = true;
                    break;
                case StateProcessResult.FAILURE:
                    HasFailures = true;
                    break;
                case StateProcessResult.WAITING:
                    //stay in current state
                    return EventProcessResult.WAITING;
                case StateProcessResult.SUCCESS:
                default:
                    break;
            }
            CurrentState = nextState;
        }
        return IsCancelled ? EventProcessResult.CANCELLED : HasFailures ? EventProcessResult.FAILURE : EventProcessResult.SUCCESS;
    }

    /// <summary>
    /// Handles waiting state resumption.
    /// </summary>
    public IEventSystemState? Resume() => CurrentState?.Resume();

    /// <summary>
    /// Handles waiting state abortion.
    /// </summary>
    public IEventSystemState? Cancel() => CurrentState?.Cancel();
    public IEventSystemState? Fail() => CurrentState?.Fail();

    internal static EventSystemContext Create(LSESDispatcher dispatcher, ILSEvent @event, IReadOnlyList<IHandlerEntry> handlers) {
        return new EventSystemContext(dispatcher, @event, handlers);
    }
}
