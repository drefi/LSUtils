using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    public abstract class PhaseState {
        protected object _lock = new();
        protected BusinessState _context;
        protected List<PhaseHandlerEntry> _handlers = new();
        protected PhaseHandlerEntry? _currentHandler;
        protected readonly Stack<PhaseHandlerEntry> _remainingHandlers = new();
        protected Dictionary<IHandlerEntry, HandlerProcessResult> _handlerResults = new();
        //public virtual EventSystemPhase Phase { get; }
        public PhaseProcessResult PhaseResult { get; protected set; } = PhaseProcessResult.UNKNOWN;
        //public StateProcessResult StateResult { get; protected set; } = StateProcessResult.CONTINUE;
        public virtual bool HasFailures => _handlerResults.Where(x => x.Value == HandlerProcessResult.FAILURE).Any();
        public virtual bool IsWaiting => _handlerResults.Where(x => x.Value == HandlerProcessResult.WAITING).Any();
        public virtual bool IsCancelled => _handlerResults.Where(x => x.Value == HandlerProcessResult.CANCELLED).Any();
        public PhaseState(BusinessState context, List<PhaseHandlerEntry> handlers) {
            _context = context;
            _handlers = handlers;
            foreach (var handler in handlers
                .Where(h => h.PhaseType == GetType())
                .OrderBy(h => h.Priority)) {
                _remainingHandlers.Push(handler);
            }
        }
        public abstract PhaseState? Process();
        public virtual PhaseState? Resume() { return null; }
        public virtual PhaseState? Cancel() { return null; }
        public virtual PhaseState? Fail() { return null; }

        protected virtual HandlerProcessResult processCurrentHandler(PhaseHandlerEntry handlerEntry) {
            if (handlerEntry == null) return HandlerProcessResult.UNKNOWN;
            if (!_handlerResults.ContainsKey(handlerEntry)) _handlerResults[handlerEntry] = HandlerProcessResult.UNKNOWN;

            // Check condition if present, if condition not met skip this handler
            if (!handlerEntry.Condition(_context._context.Event, handlerEntry)) return HandlerProcessResult.SUCCESS;
            // Execute handler
            var result = handlerEntry.Handler(_context._context);
            //always update results
            _handlerResults[handlerEntry] = result;
            handlerEntry.ExecutionCount++;

            return result;
        }
    }
    #endregion
}
