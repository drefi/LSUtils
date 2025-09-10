using System.Collections.Generic;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    public class CleanupPhaseState : PhaseState {
        //public override EventSystemPhase Phase => EventSystemPhase.CLEANUP;
        public CleanupPhaseState(BusinessState context, List<PhaseHandlerEntry> handlers) : base(context, handlers) { }

        //if at least one handler succeeds the event is considered successful, waiting don't block handler execution nor phase execution (event status is not affected);
        public override PhaseState? Process() {
            lock (_lock) {
                while (_remainingHandlers.Count > 0) {
                    _currentHandler = _remainingHandlers.Pop();
                    var result = processCurrentHandler(_currentHandler);
                    if (result == HandlerProcessResult.CANCELLED) Cancel();
                }
                PhaseResult = HasFailures ? PhaseProcessResult.FAILURE : PhaseProcessResult.CONTINUE;
                //StateResult = StateProcessResult.CONTINUE;
            }
            return null; // End of phases
        }
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            _context.StateResult = StateProcessResult.CANCELLED;
            return null; // End phase immediately
        }
    }
    #endregion
}
