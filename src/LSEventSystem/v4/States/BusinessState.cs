using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Business state implementation for v4.
/// Handles the sequential execution of business phases: VALIDATE -> CONFIGURE -> EXECUTE -> CLEANUP.
/// </summary>
public class BusinessState : IEventSystemState {
    protected EventSystemContext _context;
    PhaseState? _currentPhaseState = null;
    List<PhaseState> _phases = new();
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    public bool IsWaiting => _currentPhaseState?.IsWaiting ?? false;
    public bool HasFailures => _currentPhaseState?.HasFailures ?? false;
    public BusinessState(EventSystemContext context) {
        _context = context;
        var handlers = _context.Handlers.OfType<PhaseHandlerEntry>().ToList();
        var validate = new ValidatePhaseState(this, handlers);
        var configure = new ConfigurePhaseState(this, handlers, validate);
        var execute = new ExecutePhaseState(this, handlers, configure);
        var cleanup = new CleanupPhaseState(this, handlers, execute);
        _phases.Add(validate);
        _phases.Add(configure);
        _phases.Add(execute);
        _phases.Add(cleanup);
        _currentPhaseState = validate;
    }

    public IEventSystemState Process() {
        IEventSystemState result = this;
        if (_currentPhaseState == null) return new SucceedState(_context); //all phases completed successfully move to succeed state
        do {
            result = phaseProcess(_currentPhaseState.Process);
        } while (_currentPhaseState != null);

        return result; //all phases completed successfully move to succeed state
    }
    internal PhaseState? getPhaseState(System.Type type) {
        return _phases.FirstOrDefault(p => p.GetType() == type);
    }
    internal T? getPhaseState<T>() where T : PhaseState {
        return getPhaseState(typeof(T)) as T;
    }
    internal bool tryGetPhaseState<T>(out T? phaseState) where T : PhaseState {
        phaseState = getPhaseState<T>();
        return phaseState != null;
    }
    public IEventSystemState Resume() {
        IEventSystemState result = this;
        if (_currentPhaseState == null) return new SucceedState(_context); //all phases completed successfully move to succeed state
        do {
            result = phaseProcess(_currentPhaseState.Resume);
        } while (_currentPhaseState != null);

        return result; //all phases completed successfully move to succeed state
    }
    IEventSystemState phaseProcess(Func<PhaseState?> callback) {
        if (_currentPhaseState == null) {
            StateResult = StateProcessResult.CONTINUE;
            return new SucceedState(_context); //all phases completed successfully move to succeed state
        }
        var nextPhase = callback();
        //check if there are no more phases to proccess, if so move to succeed state
        var processPhaseResult = _currentPhaseState.PhaseResult;

        //no more phases to process check the result of the phase to determine next state
        if (nextPhase == null) {
            switch (processPhaseResult) {
                case PhaseProcessResult.CANCELLED: //when phase is cancelled move to cancelled state
                    StateResult = StateProcessResult.CANCELLED;
                    return new CancelledState(_context);
                case PhaseProcessResult.FAILURE: //when phase fails move to completed state
                    StateResult = StateProcessResult.FAILURE;
                    return new CompletedState(_context);
                case PhaseProcessResult.WAITING: //this can happen when is the last phase but a handler is still waiting
                    StateResult = StateProcessResult.WAITING;
                    return this;
                case PhaseProcessResult.CONTINUE: //when phase completes successfully move to succeed state
                default:
                    StateResult = StateProcessResult.CONTINUE;
                    return new SucceedState(_context);
            }
        }
        //there are more phases to process, check the result of the current phase to determine if can continue
        if (processPhaseResult == PhaseProcessResult.CONTINUE ||
            processPhaseResult == PhaseProcessResult.FAILURE)
            //update current phase
            _currentPhaseState = nextPhase;
        else if (processPhaseResult == PhaseProcessResult.CANCELLED)
            // when phase is cancelled move to cleanup state
            // validade phase when cancelled will skip to completed state
            _currentPhaseState = getPhaseState<CleanupPhaseState>();
        else if (processPhaseResult == PhaseProcessResult.WAITING) {
            //waiting don't change phase
            StateResult = StateProcessResult.WAITING;
            return this;
        }
        return this;
    }
    public IEventSystemState Cancel() {
        var result = _currentPhaseState?.Cancel();
        return new CancelledState(_context);
    }
    public IEventSystemState Fail() {
        return this;
    }

    #region Phase State
    internal abstract class PhaseState {
        protected static object _lock = new();
        protected BusinessState _state;
        protected List<PhaseHandlerEntry> _handlers = new();
        protected PhaseHandlerEntry? _currentHandler;
        protected readonly Stack<PhaseHandlerEntry> _remainingHandlers = new();
        protected Dictionary<IHandlerEntry, HandlerProcessResult> _handlerResults = new();
        public virtual EventSystemPhase Phase { get; }
        public PhaseState? PreviousPhase { get; protected set; }
        public PhaseProcessResult PhaseResult { get; protected set; } = PhaseProcessResult.UNKNOWN;
        //public StateProcessResult StateResult { get; protected set; } = StateProcessResult.CONTINUE;
        public virtual bool HasFailures => _handlerResults.Where(x => x.Value == HandlerProcessResult.FAILURE).Any();
        public virtual bool IsWaiting => _handlerResults.Where(x => x.Value == HandlerProcessResult.WAITING).Any();
        public virtual bool IsCancelled => _handlerResults.Where(x => x.Value == HandlerProcessResult.CANCELLED).Any();
        public PhaseState(BusinessState state, List<PhaseHandlerEntry> handlers, PhaseState? previous = null) {
            _state = state;
            _handlers = handlers;
            PreviousPhase = previous;
            foreach (var handler in handlers
                .Where(h => h.Phase == Phase)
                .OrderByDescending(h => h.Priority)) {
                _remainingHandlers.Push(handler);
            }
        }
        public abstract PhaseState? Process();
        public virtual PhaseState? Resume() { return this; }
        public virtual PhaseState? Cancel() { return null; }
        public virtual PhaseState? Fail() { return this; }

        protected virtual HandlerProcessResult processCurrentHandler(PhaseHandlerEntry handlerEntry) {
            if (handlerEntry == null) return HandlerProcessResult.UNKNOWN;
            if (!_handlerResults.ContainsKey(handlerEntry)) _handlerResults[handlerEntry] = HandlerProcessResult.UNKNOWN;

            // Check condition if present, if condition not met skip this handler
            if (!handlerEntry.Condition(_state._context.Event, handlerEntry)) return HandlerProcessResult.SUCCESS;
            // Execute handler
            var result = handlerEntry.Handler(_state._context);
            //always update results
            _handlerResults[handlerEntry] = result;
            handlerEntry.ExecutionCount++;

            return result;
        }
    }
    internal class ValidatePhaseState : PhaseState {
        public override EventSystemPhase Phase => EventSystemPhase.VALIDATE;
        public ValidatePhaseState(BusinessState state, List<PhaseHandlerEntry> handlers) : base(state, handlers) { }

        public override PhaseState? Process() {
            lock (_lock) {
                while (_remainingHandlers.Count > 0) {
                    _currentHandler = _remainingHandlers.Pop();
                    var result = processCurrentHandler(_currentHandler);
                    switch (result) {
                        case HandlerProcessResult.SUCCESS: // continue to next handler
                            continue;
                        case HandlerProcessResult.FAILURE://if at least one handler fails skip remaining handlers also phase fails;
                            PhaseResult = PhaseProcessResult.FAILURE;
                            //StateResult = StateProcessResult.FAILURE; //phase failure causes event to go to [completed state]
                            return this;
                        case HandlerProcessResult.WAITING:// waiting don't block handler processing but fails phase execution if not resumed;
                            continue;
                        case HandlerProcessResult.CANCELLED:
                            PhaseResult = PhaseProcessResult.CANCELLED;
                            //StateResult = StateProcessResult.FAILURE; // state failure in validate causes event to fail skipping to [event completed state] (instead of normally the [event canceled state])
                            return this; // End phase/state immediately

                    }
                }
                if (IsWaiting) { //if there is any handler waiting, it causes the event to fail;
                    PhaseResult = PhaseProcessResult.WAITING;
                    // in validate if a handler is waiting, is considered that the event has failed,
                    // the reason is that validate phase is expected to be quick and synchronous
                    // all handlers should be executed, thats why the check is done after all handlers are processed;
                    //StateResult = StateProcessResult.FAILURE;
                    return this;
                }
                //all handlers succeeded, continue to next phase
                PhaseResult = PhaseProcessResult.CONTINUE;
                //StateResult = StateProcessResult.CONTINUE;
            }
            return _state.getPhaseState<ConfigurePhaseState>();
        }

    }
    internal class ConfigurePhaseState : PhaseState {
        public override EventSystemPhase Phase => EventSystemPhase.CONFIGURE;
        public ConfigurePhaseState(BusinessState state, List<PhaseHandlerEntry> handlers, PhaseState previous) : base(state, handlers, previous) { }

        public override PhaseState? Process() {
            lock (_lock) {
                while (_remainingHandlers.Count > 0) {
                    _currentHandler = _remainingHandlers.Pop();
                    var result = processCurrentHandler(_currentHandler);
                    switch (result) {
                        case HandlerProcessResult.SUCCESS:
                        case HandlerProcessResult.FAILURE:
                            continue; // continue to next handler
                        case HandlerProcessResult.WAITING://waiting halts handler processing until resumed;
                            PhaseResult = PhaseProcessResult.WAITING;
                            //StateResult = StateProcessResult.WAITING;
                            return this;
                        case HandlerProcessResult.CANCELLED:
                            return Cancel();
                    }
                }
                //StateResult = StateProcessResult.CONTINUE;
                //all handlers failed, phase fails and event continues to next state
                if (_handlerResults.All(x => x.Value == HandlerProcessResult.FAILURE)) {
                    PhaseResult = PhaseProcessResult.FAILURE;
                    return this;
                } else {
                    PhaseResult = PhaseProcessResult.CONTINUE;
                }
            }
            return _state.getPhaseState<ExecutePhaseState>();
        }

        public override PhaseState? Resume() => Process();

        // when cancelling from configure phase, cleanup phase must run before cancelling the event
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CONTINUE;
            //StateResult = StateProcessResult.CANCELLED;
            return _state.getPhaseState<CleanupPhaseState>();
        }
    }
    internal class ExecutePhaseState : PhaseState {
        public override EventSystemPhase Phase => EventSystemPhase.EXECUTE;
        int _waitingHandlers = 0;
        bool _hasFailures = false;
        public override bool HasFailures => _hasFailures;
        public ExecutePhaseState(BusinessState state, List<PhaseHandlerEntry> handlers, PhaseState previous) : base(state, handlers, previous) { }


        public override PhaseState? Process() {
            lock (_lock) {
                while (_remainingHandlers.Count > 0) {
                    _currentHandler = _remainingHandlers.Pop();
                    var result = processCurrentHandler(_currentHandler);
                    switch (result) {
                        case HandlerProcessResult.SUCCESS:
                            continue; // Success continue to next handler
                        case HandlerProcessResult.FAILURE:// failed handler continue next handler;
                            _hasFailures = true;
                            continue; // Failure continue to next handler, don't skip remaining handlers
                        case HandlerProcessResult.WAITING:// waiting don't block handler process but halt phase execution until resumed;
                            _waitingHandlers++;
                            continue;
                        case HandlerProcessResult.CANCELLED: //cancelled handler exit immediatly to cancelled state
                            return Cancel();
                    }
                }
                if (IsWaiting) {
                    PhaseResult = PhaseProcessResult.WAITING;
                    //StateResult = StateProcessResult.WAITING;
                    return this;
                }
                PhaseResult = HasFailures ? PhaseProcessResult.FAILURE : PhaseProcessResult.CONTINUE;
                //StateResult = StateProcessResult.CONTINUE;
            }
            return _state.getPhaseState<CleanupPhaseState>();
        }
        public override PhaseState? Resume() {
            _waitingHandlers--;
            if (_waitingHandlers == 0) {
                //all waiting handlers have resumed, continue processing
                PhaseResult = HasFailures ? PhaseProcessResult.FAILURE : PhaseProcessResult.CONTINUE;
                //StateResult = StateProcessResult.CONTINUE;
                return _state.getPhaseState<CleanupPhaseState>();
            }
            return this;
        }
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            //StateResult = StateProcessResult.CANCELLED;
            return null;
        }
        public override PhaseState? Fail() {
            _hasFailures = true;
            PhaseResult = PhaseProcessResult.FAILURE;
            //StateResult = StateProcessResult.CONTINUE;
            return this;
        }
    }
    internal class CleanupPhaseState : PhaseState {
        public override EventSystemPhase Phase => EventSystemPhase.CLEANUP;
        public CleanupPhaseState(BusinessState state, List<PhaseHandlerEntry> handlers, PhaseState previous) : base(state, handlers, previous) { }

        //if at least one handler succeeds the event is considered successful, waiting don't block handler execution nor phase execution (event status is not affected);
        public override PhaseState? Process() {
            lock (_lock) {
                while (_remainingHandlers.Count > 0) {
                    _currentHandler = _remainingHandlers.Pop();
                    var result = processCurrentHandler(_currentHandler);
                    switch (result) {
                        case HandlerProcessResult.SUCCESS:
                        case HandlerProcessResult.FAILURE:
                        case HandlerProcessResult.WAITING:
                            continue;
                        case HandlerProcessResult.CANCELLED: //cancelled skip remaining handlers and mark event as failed
                            PhaseResult = PhaseProcessResult.CANCELLED;
                            //StateResult = StateProcessResult.FAILURE; //don't call neither cancelled nor success state
                            return null; // End phase immediately

                    }
                }
                PhaseResult = HasFailures ? PhaseProcessResult.FAILURE : PhaseProcessResult.CONTINUE;
                //StateResult = StateProcessResult.CONTINUE;
            }
            return null; // End of phases
        }
    }
    #endregion
}
