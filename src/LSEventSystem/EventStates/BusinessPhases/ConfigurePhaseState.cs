using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    /// <summary>
    /// Manages the configuration phase of business event processing.
    /// 
    /// The configure phase is responsible for resource allocation, system setup,
    /// and preparation tasks required before business logic execution. Unlike
    /// the validation phase, configuration handlers can fail without terminating
    /// the entire event - the phase continues until all handlers complete.
    /// 
    /// Phase Characteristics:
    /// - Fault Tolerant: Individual handler failures don't stop processing
    /// - Partial Success: Phase succeeds unless ALL handlers fail
    /// - Asynchronous Support: Handlers can enter waiting state
    /// - Resource Setup: Allocates resources, prepares data, configures state
    /// 
    /// Handler Result Processing:
    /// - SUCCESS: Normal completion, continue to next handler
    /// - FAILURE: Record failure but continue processing other handlers
    /// - WAITING: Suspend phase processing until Resume() is called
    /// - CANCELLED: Transition to cleanup and terminate event
    /// 
    /// Phase Success Criteria:
    /// - At least one handler succeeds OR no handlers present
    /// - Complete failure only when ALL handlers fail
    /// - Waiting handlers don't affect success determination
    /// 
    /// Common Configuration Tasks:
    /// - Database connection setup
    /// - External service initialization
    /// - Cache preparation and warming
    /// - Resource allocation (memory, file handles)
    /// - State machine initialization
    /// - Third-party API authentication
    /// - Configuration loading and validation
    /// 
    /// Architecture Notes:
    /// Configuration phase balances reliability with flexibility. The fault-tolerant
    /// design ensures that partial configuration failures don't prevent business
    /// logic execution, while still providing feedback about configuration issues.
    /// </summary>
    public class ConfigurePhaseState : PhaseState {
        //public override EventSystemPhase Phase => EventSystemPhase.CONFIGURE;
        int _waitingHandlers = 0;
        
        /// <summary>
        /// Constructs a new ConfigurePhaseState with the specified context and handlers.
        /// 
        /// Initializes the configuration phase with handlers responsible for
        /// resource allocation, system setup, and preparation tasks before
        /// business logic execution.
        /// </summary>
        /// <param name="context">The BusinessState context managing phase transitions</param>
        /// <param name="handlers">Collection of configuration handlers to execute</param>
        public ConfigurePhaseState(BusinessState context, List<LSPhaseHandlerEntry> handlers) : base(context, handlers) { }

        /// <summary>
        /// Processes the configuration phase with fault-tolerant execution.
        /// 
        /// The configuration phase uses a fault-tolerant strategy where individual
        /// handler failures don't terminate processing. This ensures that partial
        /// configuration problems don't prevent business logic execution.
        /// 
        /// Execution Flow:
        /// 1. Execute handlers sequentially in priority order
        /// 2. On SUCCESS/FAILURE: Continue to next handler (both recorded)
        /// 3. On WAITING: Suspend processing, increment waiting counter
        /// 4. On CANCELLED: Transition to cleanup phase
        /// 5. Post-processing: Evaluate overall phase success
        /// 6. Success: Transition to ExecutePhaseState
        /// 7. Complete failure: Transition to next state (typically cleanup)
        /// 
        /// Handler Result Processing:
        /// - SUCCESS: Positive result, continue processing
        /// - FAILURE: Negative result but processing continues
        /// - WAITING: Suspend phase, wait for Resume() call
        /// - CANCELLED: Immediate transition to cleanup
        /// 
        /// Waiting Handler Management:
        /// Configuration supports asynchronous operations. When a handler
        /// returns WAITING, processing suspends and can be resumed later.
        /// The waiting counter tracks pending operations.
        /// 
        /// Phase Success Evaluation:
        /// - SUCCESS: At least one handler succeeded or no failures recorded
        /// - FAILURE: ALL handlers failed (rare due to fault tolerance)
        /// - Phase continues to execute even with partial failures
        /// 
        /// Configuration Examples:
        /// - Database connection pool setup
        /// - External API authentication
        /// - Cache initialization and warming
        /// - Resource allocation (memory, threads)
        /// - Configuration file loading
        /// - State machine preparation
        /// - Service discovery and registration
        /// 
        /// Fault Tolerance Design:
        /// The forgiving failure handling ensures system resilience. Many
        /// configuration tasks are optional or have fallback mechanisms,
        /// so partial failures shouldn't prevent core business logic execution.
        /// </summary>
        /// <returns>
        /// ExecutePhaseState on success, CleanupPhaseState on cancellation, 
        /// or this instance if waiting for async operations
        /// </returns>
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

        /// <summary>
        /// Resumes configuration phase processing after waiting handlers complete.
        /// 
        /// When handlers enter a WAITING state (typically for asynchronous operations),
        /// this method is called to resume processing. The waiting counter is decremented
        /// and processing continues if all waiting operations have completed.
        /// 
        /// Resume Logic:
        /// 1. Decrement waiting handler counter
        /// 2. If counter becomes negative: Early resume detected, stay in waiting
        /// 3. If counter reaches zero: All operations complete, continue processing
        /// 4. Call Process() to continue with remaining handlers
        /// 
        /// Early Resume Handling:
        /// If Resume() is called before a handler actually enters waiting state,
        /// the counter becomes negative. This indicates a timing issue where
        /// the resume signal arrived before the wait signal was processed.
        /// The phase remains in waiting state to handle this race condition.
        /// 
        /// Synchronization:
        /// The waiting counter provides basic synchronization for async operations.
        /// Each WAITING result increments the counter, each Resume() decrements it.
        /// Processing continues only when the counter reaches zero.
        /// 
        /// Common Resume Scenarios:
        /// - Database connection established
        /// - External API authentication completed  
        /// - Resource allocation finished
        /// - Configuration loading completed
        /// - Cache warming finished
        /// - Service initialization completed
        /// </summary>
        /// <returns>
        /// Result of continued processing, or this instance if still waiting
        /// </returns>
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

        /// <summary>
        /// Handles cancellation requests during configuration processing.
        /// 
        /// When cancellation is requested during configuration, the phase ensures
        /// proper cleanup by transitioning to the cleanup phase rather than
        /// terminating immediately. This allows allocated resources to be
        /// properly released and cleanup handlers to execute.
        /// 
        /// Cancellation Flow:
        /// 1. Set phase result to CANCELLED
        /// 2. Set business state result to CANCELLED
        /// 3. Transition to CleanupPhaseState for resource cleanup
        /// 4. Cleanup phase handles final cleanup and termination
        /// 
        /// Resource Cleanup Guarantee:
        /// Unlike other phases that may terminate immediately on cancellation,
        /// the configuration phase ensures cleanup handlers run. This is critical
        /// because configuration may have allocated resources (connections,
        /// memory, locks) that must be properly released.
        /// 
        /// Cleanup Transition:
        /// The transition to cleanup phase ensures:
        /// - Allocated resources are properly released
        /// - Cleanup handlers execute in proper order
        /// - System state is left clean after cancellation
        /// - No resource leaks occur from partial configuration
        /// 
        /// Common Cancellation Scenarios:
        /// - User-initiated cancellation during setup
        /// - Timeout during resource allocation
        /// - External dependency failure
        /// - System shutdown requests
        /// - Critical error detection requiring immediate stop
        /// </summary>
        /// <returns>CleanupPhaseState to ensure proper resource cleanup</returns>
        // when cancelling from configure phase, cleanup phase must run before cancelling the event
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            _context.StateResult = StateProcessResult.CANCELLED;
            return _context.getPhaseState<CleanupPhaseState>();
        }
    }
    #endregion
}
