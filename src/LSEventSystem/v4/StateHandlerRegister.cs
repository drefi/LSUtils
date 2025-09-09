using System;

namespace LSUtils.EventSystem;

public class StateHandlerRegister<TEvent> where TEvent : ILSEvent {
    private readonly LSESDispatcher _dispatcher;
    protected LSESPriority _priority = LSESPriority.NORMAL;
    protected System.Type _stateType = typeof(BusinessState);
    protected Func<ILSEvent, IHandlerEntry, bool> _condition = (evt, entry) => true;

    internal StateHandlerRegister(LSESDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }
    public StateHandlerRegister<TEvent> OnState<TState>() where TState : IEventSystemState {
        _stateType = typeof(TState);
        return this;

    }
    public StateHandlerRegister<TEvent> WithPriority(LSESPriority priority) {
        _priority = priority;
        return this;
    }
    public StateHandlerRegister<TEvent> When(Func<ILSEvent, IHandlerEntry, bool> condition) {
        if (condition == null) throw new LSArgumentNullException(nameof(condition));
        _condition += condition;
        return this;
    }
    public System.Guid Register(LSAction<TEvent> handler) {
        var entry = new StateHandlerEntry {
            Priority = _priority,
            StateType = _stateType,
            Handler = evt => handler((TEvent)evt),
            Condition = _condition
        };

        return _dispatcher.registerHandler(typeof(TEvent), entry);
    }

}
