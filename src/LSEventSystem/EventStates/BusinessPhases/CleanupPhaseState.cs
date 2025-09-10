using System.Collections.Generic;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    /// <summary>
    /// Final phase in the business processing pipeline responsible for cleanup and finalization.
    /// 
    /// The CleanupPhaseState represents the last phase of business processing, executing
    /// regardless of the success or failure of previous phases. This phase is responsible
    /// for resource cleanup, finalization operations, and ensuring the system is left
    /// in a consistent state.
    /// 
    /// Key Characteristics:
    /// - Always executes, regardless of previous phase outcomes
    /// - Cannot enter waiting state (all operations must be synchronous)
    /// - Handler failures don't prevent phase completion
    /// - Cancellation immediately terminates the phase
    /// - Final phase - returns null to end business processing
    /// 
    /// Handler Execution Model:
    /// - All handlers execute regardless of individual failures
    /// - Cancellation handlers can terminate phase immediately
    /// - No waiting support - operations must complete synchronously
    /// - Best effort execution - continues on non-critical failures
    /// 
    /// Cleanup Responsibilities:
    /// - Resource disposal and memory cleanup
    /// - Temporary file and cache cleanup
    /// - Database connection and transaction cleanup
    /// - External service cleanup and disconnection
    /// - Logging final phase completion status
    /// - Metrics and performance data collection
    /// 
    /// Error Handling:
    /// - Individual handler failures are logged but don't stop cleanup
    /// - Only cancellation can prevent full cleanup execution
    /// - Phase marked as failure if any handlers fail, but processing continues
    /// - Critical for system stability and resource management
    /// 
    /// State Transitions:
    /// - Entry: From ExecutePhaseState or CancelledState scenarios
    /// - Exit: Always returns null (end of business phases)
    /// - No next phase - transitions handled by BusinessState
    /// </summary>
    public class CleanupPhaseState : PhaseState {
        /// <summary>
        /// Constructs a new CleanupPhaseState with the specified context and handlers.
        /// 
        /// Initializes the final phase with cleanup-specific handlers that will
        /// execute regardless of previous phase outcomes. This ensures consistent
        /// resource cleanup and system finalization.
        /// </summary>
        /// <param name="context">The BusinessState context managing phase transitions</param>
        /// <param name="handlers">Collection of cleanup handlers to execute</param>
        public CleanupPhaseState(BusinessState context, List<LSPhaseHandlerEntry> handlers) : base(context, handlers) { }

        /// <summary>
        /// Processes the cleanup phase by executing all cleanup handlers.
        /// 
        /// The cleanup phase uses a "best effort" execution model where handler
        /// failures don't prevent other handlers from executing. This ensures
        /// maximum cleanup coverage even when individual operations fail.
        /// 
        /// Execution Flow:
        /// 1. Execute all handlers in priority order
        /// 2. Continue processing even if handlers fail
        /// 3. Only stop on cancellation (CANCELLED result)
        /// 4. Mark phase result based on handler outcomes
        /// 5. Return null to end business phase processing
        /// 
        /// Handler Processing:
        /// - SUCCESS/FAILURE: Continue to next handler
        /// - CANCELLED: Terminate phase immediately with cancellation
        /// - WAITING: Not supported - treated as SUCCESS
        /// 
        /// Cleanup Operations:
        /// - Resource disposal (connections, files, memory)
        /// - Temporary data cleanup
        /// - Cache invalidation and cleanup
        /// - External service disconnection
        /// - Final logging and metrics collection
        /// - Error state cleanup and recovery
        /// 
        /// Phase Result Determination:
        /// - FAILURE: If any handlers failed (but processing completed)
        /// - CONTINUE: If all handlers succeeded
        /// - CANCELLED: If any handler requested cancellation
        /// 
        /// Critical Design:
        /// This phase is critical for system stability and must execute
        /// even when previous phases fail. It ensures the system remains
        /// in a consistent, clean state regardless of business logic outcomes.
        /// </summary>
        /// <returns>Always null to indicate end of business phase processing</returns>
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
        
        /// <summary>
        /// Handles cancellation requests during cleanup processing.
        /// 
        /// When cancellation is requested during cleanup, the phase is marked
        /// as cancelled and processing terminates immediately. This ensures
        /// that critical cancellation scenarios can interrupt even cleanup operations.
        /// 
        /// Cancellation Effects:
        /// - Sets phase result to CANCELLED
        /// - Sets business state result to CANCELLED
        /// - Terminates phase processing immediately
        /// - No further handlers execute
        /// 
        /// Use Cases:
        /// - Critical system shutdown scenarios
        /// - Emergency termination requirements
        /// - Timeout scenarios that must be respected
        /// - Security-related immediate termination
        /// </summary>
        /// <returns>Always null to indicate immediate phase termination</returns>
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            _context.StateResult = StateProcessResult.CANCELLED;
            return null; // End phase immediately
        }
    }
    #endregion
}
