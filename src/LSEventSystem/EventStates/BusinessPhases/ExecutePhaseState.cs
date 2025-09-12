using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

public partial class LSEventBusinessState {
    #region Phase State
    /// <summary>
    /// Manages the execution phase of business event processing.
    /// 
    /// The execute phase is the core business logic phase where the primary
    /// work of the event is performed. This phase is designed for fault tolerance
    /// with comprehensive failure tracking and asynchronous operation support.
    /// 
    /// Phase Characteristics:
    /// - Fault Tolerant: Individual handler failures are recorded but don't stop processing
    /// - Failure Tracking: Maintains detailed failure state for reporting
    /// - Asynchronous Support: Full support for waiting/resuming operations
    /// - Comprehensive Processing: All handlers execute regardless of individual failures
    /// 
    /// Handler Result Processing:
    /// - SUCCESS: Normal completion, continue to next handler
    /// - FAILURE: Record failure but continue processing other handlers
    /// - WAITING: Suspend phase processing until Resume() or Fail() is called
    /// - CANCELLED: Immediate termination without cleanup transition
    /// 
    /// Phase Success Criteria:
    /// - SUCCESS: All handlers completed without failures
    /// - FAILURE: At least one handler failed (partial failure)
    /// - Phase always processes all handlers unless cancelled
    /// 
    /// Failure Handling Philosophy:
    /// The execute phase prioritizes comprehensive execution over fail-fast behavior.
    /// This ensures maximum work completion and detailed failure reporting for
    /// business analysis and troubleshooting.
    /// 
    /// Common Execution Tasks:
    /// - Core business rule processing
    /// - Data transformation and calculation
    /// - External service integration
    /// - Database operations (CRUD)
    /// - File processing and generation
    /// - Email and notification sending
    /// - Complex workflow orchestration
    /// - Business validation and enforcement
    /// 
    /// Architecture Notes:
    /// Execute phase is the heart of the event system. It balances reliability
    /// with thorough execution, ensuring business logic runs completely while
    /// maintaining detailed failure tracking for operational monitoring.
    /// </summary>
    public class ExecutePhaseState : PhaseState {
        //public override EventSystemPhase Phase => EventSystemPhase.EXECUTE;
        int _waitingHandlers = 0;
        bool _hasFailures = false;

        /// <summary>
        /// Indicates whether this phase has handlers in waiting state.
        /// 
        /// ExecutePhase overrides the base IsWaiting to check for pending
        /// asynchronous operations tracked by the waiting counter.
        /// </summary>
        public override bool IsWaiting => _waitingHandlers > 0;

        /// <summary>
        /// Indicates whether any handlers in this phase have failed.
        /// 
        /// This property provides immediate access to failure state without
        /// requiring evaluation of all handler results. It's updated in real-time
        /// as handlers complete with failure results.
        /// </summary>
        public override bool HasFailures => _hasFailures;

        /// <summary>
        /// Constructs a new ExecutePhaseState with the specified context and handlers.
        /// 
        /// Initializes the execution phase with handlers responsible for core
        /// business logic processing, data transformation, and primary event work.
        /// </summary>
        /// <param name="context">The BusinessState context managing phase transitions</param>
        /// <param name="handlers">Collection of execution handlers to process</param>
        public ExecutePhaseState(LSEventBusinessState context, List<LSPhaseHandlerEntry> handlers) : base(context, handlers) { }

        /// <summary>
        /// Processes the execution phase with comprehensive failure tracking.
        /// 
        /// The execution phase uses a comprehensive processing strategy where all
        /// handlers execute regardless of individual failures. This ensures maximum
        /// work completion and detailed failure reporting for business analysis.
        /// 
        /// Execution Flow:
        /// 1. Execute handlers sequentially in priority order
        /// 2. On SUCCESS: Continue to next handler
        /// 3. On FAILURE: Record failure, continue processing
        /// 4. On WAITING: Increment waiting counter, continue with remaining handlers
        /// 5. On CANCELLED: Immediate termination
        /// 6. Post-processing: Check for waiting handlers
        /// 7. Continue: Transition to CleanupPhaseState
        /// 
        /// Handler Result Processing:
        /// - SUCCESS: Positive result, continue processing
        /// - FAILURE: Set failure flag, continue processing (comprehensive execution)
        /// - WAITING: Increment waiting counter, continue processing other handlers
        /// - CANCELLED: Immediate termination without cleanup
        /// 
        /// Waiting Handler Management:
        /// Execute phase supports complex asynchronous operations. Handlers can
        /// enter waiting state while other handlers continue processing. The
        /// phase completes only when all handlers finish AND all waiting
        /// operations are resolved.
        /// 
        /// Phase Result Determination:
        /// - WAITING: Any handlers are still in waiting state
        /// - FAILURE: At least one handler failed (after all complete)
        /// - CONTINUE: All handlers succeeded
        /// 
        /// Comprehensive Execution Benefits:
        /// - Complete work execution despite partial failures
        /// - Detailed failure reporting for business analysis
        /// - Maximum value extraction from successful operations
        /// - Comprehensive audit trail for troubleshooting
        /// 
        /// Execution Examples:
        /// - Customer order processing and fulfillment
        /// - Financial transaction execution
        /// - Data import and transformation
        /// - Business rule validation and enforcement
        /// - Integration with multiple external systems
        /// - Complex calculation and analysis
        /// - Workflow state transitions
        /// 
        /// Performance Considerations:
        /// Execute phase prioritizes thoroughness over speed. All handlers run
        /// to completion unless explicitly cancelled, ensuring comprehensive
        /// business processing at the cost of potential execution time.
        /// </summary>
        /// <returns>
        /// CleanupPhaseState when all handlers complete, this instance if waiting,
        /// or null if cancelled
        /// </returns>
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
            return _stateContext.getPhaseState<CleanupPhaseState>();
        }

        /// <summary>
        /// Resumes execution phase processing after waiting handlers complete successfully.
        /// 
        /// When handlers complete asynchronous operations successfully, this method
        /// is called to resume processing. It manages the waiting counter and determines
        /// whether all operations have completed.
        /// 
        /// Resume Logic:
        /// 1. Decrement waiting handler counter
        /// 2. If counter reaches zero: All async operations complete
        /// 3. Determine final phase result based on failure state
        /// 4. Transition to cleanup phase
        /// 5. If still waiting: Return to waiting state
        /// 
        /// Counter Management:
        /// The waiting counter tracks pending asynchronous operations. Each
        /// WAITING result increments it, each Resume() decrements it. When
        /// the counter reaches zero, all async work is complete.
        /// 
        /// Final Result Determination:
        /// - HasFailures = true: Phase result is FAILURE
        /// - HasFailures = false: Phase result is CONTINUE
        /// - Transition to cleanup phase for resource management
        /// 
        /// Timing Considerations:
        /// If Resume() is called before a handler enters waiting state,
        /// the counter becomes negative. This is handled gracefully by
        /// continuing to wait until the counter balances.
        /// 
        /// Success Scenarios:
        /// - Async database operations complete successfully
        /// - External API calls return with success
        /// - File processing operations finish
        /// - Background calculations complete
        /// - Message queue operations succeed
        /// </summary>
        /// <returns>
        /// CleanupPhaseState if all operations complete, or this instance if still waiting
        /// </returns>
        public override PhaseState? Resume() {
            if (IsCancelled) return Cancel();
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
                return _stateContext.getPhaseState<CleanupPhaseState>();
            }
            PhaseResult = PhaseProcessResult.WAITING;
            return this;
        }

        /// <summary>
        /// Handles cancellation requests during execution processing.
        /// 
        /// When cancellation is requested during execution, the phase terminates
        /// immediately without transitioning to cleanup. This represents an
        /// emergency stop where immediate termination is required.
        /// 
        /// Cancellation Effects:
        /// - Sets phase result to CANCELLED
        /// - Returns null to indicate immediate termination
        /// - Does NOT transition to cleanup phase
        /// - Bypasses normal cleanup procedures
        /// 
        /// Emergency Termination:
        /// Unlike other phases, execute phase cancellation does not guarantee
        /// cleanup execution. This is designed for scenarios where immediate
        /// termination is critical and cleanup might be unsafe or unnecessary.
        /// 
        /// Use Cases for Immediate Termination:
        /// - Critical system errors requiring immediate stop
        /// - Security breaches requiring emergency shutdown
        /// - Resource exhaustion requiring immediate halt
        /// - User-requested emergency cancellation
        /// - Timeout scenarios with strict deadlines
        /// 
        /// Resource Considerations:
        /// Immediate termination may leave resources in an inconsistent state.
        /// This trade-off prioritizes rapid response over cleanup guarantees.
        /// </summary>
        /// <returns>Always null to indicate immediate termination</returns>
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            return null;
        }

        /// <summary>
        /// Handles failure notifications for waiting handlers during execution.
        /// 
        /// When asynchronous operations fail during execution, this method is called
        /// to handle the failure and manage the waiting state. It balances failure
        /// tracking with continued processing of remaining operations.
        /// 
        /// Failure Processing Logic:
        /// 1. Decrement waiting handler counter (failed operation completed)
        /// 2. Set failure flag to track that failures occurred
        /// 3. If counter reaches zero: All async operations complete
        /// 4. Set phase result to FAILURE and transition to cleanup
        /// 5. If still waiting: Continue waiting for remaining operations
        /// 
        /// Comprehensive Failure Handling:
        /// Even when failures occur, the phase waits for all async operations
        /// to complete. This ensures:
        /// - Complete failure reporting
        /// - Proper resource cleanup from successful operations
        /// - Comprehensive audit trail
        /// - No orphaned async operations
        /// 
        /// Counter Management:
        /// The waiting counter tracks all pending operations, both successful
        /// and failed. Each operation must complete (success or failure) before
        /// the phase can transition to cleanup.
        /// 
        /// Failure State Tracking:
        /// The _hasFailures flag is set to ensure the final phase result
        /// reflects the failure state, even if subsequent operations succeed.
        /// 
        /// Common Failure Scenarios:
        /// - Async database operations timeout or fail
        /// - External API calls return errors
        /// - File operations encounter I/O errors
        /// - Network operations fail or timeout
        /// - Background calculations encounter errors
        /// - Message queue operations fail
        /// 
        /// Operational Benefits:
        /// - Complete failure reporting for troubleshooting
        /// - Proper cleanup of partial successes
        /// - Comprehensive operational monitoring
        /// - No resource leaks from incomplete operations
        /// </summary>
        /// <returns>
        /// CleanupPhaseState if all operations complete, or this instance if still waiting
        /// </returns>
        public override PhaseState? Fail() {
            _waitingHandlers--;
            if (_waitingHandlers == 0) {
                //all waiting handlers have resumed, continue processing
                PhaseResult = PhaseProcessResult.FAILURE;
                return _stateContext.getPhaseState<CleanupPhaseState>();
            }
            _hasFailures = true;
            PhaseResult = PhaseProcessResult.WAITING;
            return this;
        }
    }
    #endregion
}
