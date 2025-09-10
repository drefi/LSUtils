using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    public class ConfigurePhaseState : PhaseState {
        //public override EventSystemPhase Phase => EventSystemPhase.CONFIGURE;
        int _waitingHandlers = 0;
        public ConfigurePhaseState(BusinessState context, List<PhaseHandlerEntry> handlers) : base(context, handlers) { }

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
                            _waitingHandlers++;
                            if (_waitingHandlers == 0) {
                                // so for this particular handler we consider it has already resumed
                                // this can cause a issue where this is not the handler that was resumed
                                // but in practice this should not be a problem, because handler results are only used to determine the outcome of the phase
                                _handlerResults[_currentHandler] = HandlerProcessResult.SUCCESS;
                                continue;
                            }
                            PhaseResult = PhaseProcessResult.WAITING;
                            _context.StateResult = StateProcessResult.WAITING;
                            return this;
                        case HandlerProcessResult.CANCELLED:
                            return Cancel();
                    }
                }
                //StateResult = StateProcessResult.CONTINUE;
                //all handlers failed, phase fails and event continues to next state
                if (_handlerResults.Count > 0 && _handlerResults.All(x => x.Value == HandlerProcessResult.FAILURE)) {
                    PhaseResult = PhaseProcessResult.FAILURE;
                    return null; // all handlers failed, phase fails and event continues to next state
                } else {
                    PhaseResult = PhaseProcessResult.CONTINUE;
                }
            }
            return _context.getPhaseState<ExecutePhaseState>();
        }

        public override PhaseState? Resume() {
            _waitingHandlers--;
            // if count is negative it means Resume was called before the handler actually went to waiting state
            if (_waitingHandlers < 0) {
                PhaseResult = PhaseProcessResult.WAITING;
                //there are still handlers waiting, don't continue processing
                return this;
            }
            return Process();
        }

        // when cancelling from configure phase, cleanup phase must run before cancelling the event
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            _context.StateResult = StateProcessResult.CANCELLED;
            return _context.getPhaseState<CleanupPhaseState>();
        }
    }
    #endregion
}
