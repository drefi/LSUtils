using System.Collections.Generic;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    public class ValidatePhaseState : PhaseState {
        //public override EventSystemPhase Phase => EventSystemPhase.VALIDATE;
        public ValidatePhaseState(BusinessState context, List<PhaseHandlerEntry> handlers) : base(context, handlers) { }

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
                            _context.StateResult = StateProcessResult.FAILURE;
                            return null; // on validade failure skip remaining handlers and end phase & state immediately
                        case HandlerProcessResult.WAITING:// waiting don't block handler processing but fails phase execution if not resumed;
                            continue;
                        case HandlerProcessResult.CANCELLED:
                            return Cancel();

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
            }
            return _context.getPhaseState<ConfigurePhaseState>();
        }
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            _context.StateResult = StateProcessResult.CANCELLED; //this is a drible da vaca
            return null;
        }
    }
    #endregion
}
