using System.Collections.Generic;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    public class ExecutePhaseState : PhaseState {
        //public override EventSystemPhase Phase => EventSystemPhase.EXECUTE;
        int _waitingHandlers = 0;
        bool _hasFailures = false;
        public override bool HasFailures => _hasFailures;
        public ExecutePhaseState(BusinessState context, List<PhaseHandlerEntry> handlers) : base(context, handlers) { }

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
                            // in case a handler goes to waiting state after Resume was called, the count can be negative
                            if (_waitingHandlers == 0) {
                                // so for this particular handler we consider it has already resumed
                                // this can cause a issue where this is not the handler that was resumed
                                // but in practice this should not be a problem, because handler results are only used to determine the outcome of the phase
                                _handlerResults[_currentHandler] = HandlerProcessResult.SUCCESS;
                            }
                            continue;
                        case HandlerProcessResult.CANCELLED: //cancelled handler exit immediatly to cancelled state
                            return Cancel();
                    }
                }
                if (IsWaiting) {
                    PhaseResult = PhaseProcessResult.WAITING;
                    return this;
                }
                PhaseResult = HasFailures ? PhaseProcessResult.FAILURE : PhaseProcessResult.CONTINUE;
            }
            return _context.getPhaseState<CleanupPhaseState>();
        }
        public override PhaseState? Resume() {
            //decreasing the count is a way to tell how many handlers are still waiting to resume
            //if value is negative it means that Resume() was called before the handler actually went to waiting state
            //it should not be a problem, because the handler will not be processed again
            //example: if Resume() is called 2 times, and then 1 handler goes to waiting state, the count will be 1
            //          the Resume() will return the ExecutePhaseState because it still waiting
            _waitingHandlers--;
            if (_waitingHandlers == 0) {
                //all waiting handlers have resumed, continue processing
                PhaseResult = HasFailures ? PhaseProcessResult.FAILURE : PhaseProcessResult.CONTINUE;
                //StateResult = StateProcessResult.CONTINUE;
                return _context.getPhaseState<CleanupPhaseState>();
            }
            PhaseResult = PhaseProcessResult.WAITING;
            return this;
        }
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            return null;
        }
        public override PhaseState? Fail() {
            _waitingHandlers--;
            if (_waitingHandlers == 0) {
                //all waiting handlers have resumed, continue processing
                PhaseResult = PhaseProcessResult.FAILURE;
                return _context.getPhaseState<CleanupPhaseState>();
            }
            _hasFailures = true;
            PhaseResult = PhaseProcessResult.WAITING;
            return this;
        }
    }
    #endregion
}
