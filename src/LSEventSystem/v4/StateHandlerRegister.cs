using System;
namespace LSUtils.EventSystem;

public class StateHandlerRegister<TState> where TState : IEventSystemState {
    private readonly LSESDispatcher _dispatcher;
    protected LSESPriority _priority = LSESPriority.NORMAL;
    protected System.Type _stateType = typeof(TState);
    protected LSAction<ILSEvent>? _handler = null;
    protected Func<ILSEvent, IHandlerEntry, bool> _condition = (evt, entry) => true;
    public bool IsBuild { get; protected set; } = false;

    internal StateHandlerRegister(LSESDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }
    public StateHandlerRegister<TState> WithPriority(LSESPriority priority) {
        _priority = priority;
        return this;
    }
    public StateHandlerRegister<TState> When(Func<ILSEvent, IHandlerEntry, bool> condition) {
        if (condition == null) throw new LSArgumentNullException(nameof(condition));
        _condition += condition;
        return this;
    }
    public StateHandlerRegister<TState> Handler(LSAction<ILSEvent> handler) {
        _handler = handler;
        return this;
    }
    public StateHandlerEntry Build() {
        if (_handler == null) throw new LSException("handler_not_defined");
        if (_stateType == null) throw new LSArgumentNullException(nameof(_stateType));
        if (IsBuild) throw new LSException("handler_already_built");
        var entry = new StateHandlerEntry {
            Priority = _priority,
            StateType = _stateType,
            Condition = _condition,
            Handler = _handler,
        };
        IsBuild = true;
        return entry;
    }
    public System.Guid Register() {
        if (IsBuild) throw new LSException("handler_already_built");
        var entry = Build();
        return _dispatcher.registerHandler(typeof(TState), entry);
    }

}
