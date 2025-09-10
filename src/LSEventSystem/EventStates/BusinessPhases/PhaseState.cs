using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    /// <summary>
    /// Abstract base class for all business phase states in the LSEventSystem v4.
    /// 
    /// Represents a single phase in the business state processing pipeline and manages
    /// the execution of phase-specific handlers. Each phase state is responsible for:
    /// - Managing handler execution order based on priority
    /// - Tracking handler results and phase completion status
    /// - Controlling transitions to the next phase or state
    /// - Handling waiting, cancellation, and failure scenarios
    /// 
    /// Phase Execution Model:
    /// - Handlers are sorted by priority (CRITICAL → HIGH → NORMAL → LOW → BACKGROUND)
    /// - Each handler is executed sequentially within the phase
    /// - Handler results determine phase flow and transitions
    /// - Thread-safe execution with proper locking mechanisms
    /// 
    /// Concrete implementations:
    /// - <see cref="ValidatePhaseState"/>: Input validation and early checks
    /// - <see cref="ConfigurePhaseState"/>: Resource allocation and setup
    /// - <see cref="ExecutePhaseState"/>: Core business logic execution
    /// - <see cref="CleanupPhaseState"/>: Finalization and resource cleanup
    /// 
    /// State Transitions:
    /// - CONTINUE: Proceed to next phase
    /// - FAILURE: Mark as failed, continue to cleanup or failure handling
    /// - WAITING: Pause for external input
    /// - CANCELLED: Immediate termination, transition to cancelled state
    /// </summary>
    public abstract class PhaseState {
        /// <summary>
        /// Thread synchronization lock for handler execution.
        /// Ensures thread-safe access to handler state and results.
        /// </summary>
        protected object _lock = new();
        
        /// <summary>
        /// Reference to the parent BusinessState that owns this phase.
        /// Provides access to the overall event context and state management.
        /// </summary>
        protected BusinessState _context;
        
        /// <summary>
        /// Complete list of handlers available for this phase.
        /// Filtered during construction to include only handlers for this phase type.
        /// </summary>
        protected List<LSPhaseHandlerEntry> _handlers = new();
        
        /// <summary>
        /// The currently executing handler entry, if any.
        /// Used for tracking and debugging purposes.
        /// </summary>
        protected LSPhaseHandlerEntry? _currentHandler;
        
        /// <summary>
        /// Stack of handlers remaining to be executed in this phase.
        /// Handlers are pre-sorted by priority during phase construction.
        /// </summary>
        protected readonly Stack<LSPhaseHandlerEntry> _remainingHandlers = new();
        
        /// <summary>
        /// Dictionary tracking the execution result of each handler.
        /// Used to determine overall phase outcome and manage retries.
        /// </summary>
        protected Dictionary<IHandlerEntry, HandlerProcessResult> _handlerResults = new();
        
        /// <summary>
        /// The final result of this phase execution.
        /// Determines the next phase transition or state change.
        /// </summary>
        public PhaseProcessResult PhaseResult { get; protected set; } = PhaseProcessResult.UNKNOWN;
        
        /// <summary>
        /// Indicates whether any handlers in this phase have failed.
        /// Used for phase outcome evaluation and error handling.
        /// </summary>
        public virtual bool HasFailures => _handlerResults.Where(x => x.Value == HandlerProcessResult.FAILURE).Any();
        
        /// <summary>
        /// Indicates whether any handlers in this phase are in a waiting state.
        /// Determines if the phase should pause for external input.
        /// </summary>
        public virtual bool IsWaiting => _handlerResults.Where(x => x.Value == HandlerProcessResult.WAITING).Any();
        
        /// <summary>
        /// Indicates whether any handlers in this phase have been cancelled.
        /// Used to trigger immediate phase and event termination.
        /// </summary>
        public virtual bool IsCancelled => _handlerResults.Where(x => x.Value == HandlerProcessResult.CANCELLED).Any();
        /// <summary>
        /// Initializes a new phase state with the specified context and handlers.
        /// 
        /// During construction, the phase:
        /// 1. Filters handlers to include only those for this phase type
        /// 2. Sorts handlers by priority (CRITICAL → HIGH → NORMAL → LOW → BACKGROUND)
        /// 3. Pushes handlers onto the execution stack in reverse order for proper execution sequence
        /// </summary>
        /// <param name="context">The parent BusinessState providing event context</param>
        /// <param name="handlers">Complete list of phase handlers for filtering</param>
        public PhaseState(BusinessState context, List<LSPhaseHandlerEntry> handlers) {
            _context = context;
            _handlers = handlers;
            foreach (var handler in handlers
                .Where(h => h.PhaseType == GetType())
                .OrderBy(h => h.Priority)) {
                _remainingHandlers.Push(handler);
            }
        }
        /// <summary>
        /// Executes this phase and returns the next phase or null if phase processing is complete.
        /// 
        /// Each phase implementation defines its specific execution logic:
        /// - Handler execution order and flow control
        /// - Failure handling and recovery strategies  
        /// - Waiting state management for asynchronous operations
        /// - Phase transition logic based on handler results
        /// 
        /// Must be implemented by each concrete phase to define phase-specific behavior.
        /// </summary>
        /// <returns>
        /// The next phase to execute, or null if:
        /// - Phase sequence is complete
        /// - Event should transition to a different state (Success, Failure, Cancelled)
        /// - Phase is waiting for external input
        /// </returns>
        public abstract PhaseState? Process();
        
        /// <summary>
        /// Resumes processing from a waiting state.
        /// 
        /// Called when external input is received to continue phase execution.
        /// Default implementation returns null, indicating no resumption logic.
        /// Phases that support waiting operations should override this method.
        /// </summary>
        /// <returns>The next phase to execute, or null to remain in current phase</returns>
        public virtual PhaseState? Resume() { return null; }
        
        /// <summary>
        /// Cancels the current phase execution.
        /// 
        /// Triggers immediate phase termination and event cancellation.
        /// Default implementation returns null, allowing phases to define custom cancellation logic.
        /// </summary>
        /// <returns>The next phase to execute during cancellation, or null to end processing</returns>
        public virtual PhaseState? Cancel() { return null; }
        
        /// <summary>
        /// Handles phase failure scenarios.
        /// 
        /// Called when a critical failure occurs that requires immediate phase termination.
        /// Default implementation returns null, allowing phases to define custom failure handling.
        /// </summary>
        /// <returns>The next phase to execute during failure handling, or null to end processing</returns>
        public virtual PhaseState? Fail() { return null; }

        /// <summary>
        /// Executes a single handler and returns its processing result.
        /// 
        /// Handler Execution Flow:
        /// 1. Validates handler entry is not null
        /// 2. Initializes handler result tracking if not present
        /// 3. Evaluates handler condition - skips execution if condition fails
        /// 4. Executes the handler with the current event context
        /// 5. Updates handler results and execution count
        /// 6. Returns the handler execution result
        /// 
        /// Thread Safety: This method should only be called within the phase's lock.
        /// </summary>
        /// <param name="handlerEntry">The handler entry to execute</param>
        /// <returns>
        /// The result of handler execution:
        /// - SUCCESS: Handler completed successfully
        /// - FAILURE: Handler failed but processing can continue
        /// - WAITING: Handler requires external input
        /// - CANCELLED: Handler requests immediate cancellation
        /// - UNKNOWN: Handler entry was null or invalid
        /// </returns>
        protected virtual HandlerProcessResult processCurrentHandler(LSPhaseHandlerEntry handlerEntry) {
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
