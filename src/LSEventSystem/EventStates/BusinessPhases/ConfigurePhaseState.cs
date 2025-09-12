using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

public partial class LSEventBusinessState {
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
        public ConfigurePhaseState(LSEventBusinessState context, List<LSPhaseHandlerEntry> handlers) : base(context, handlers) { }

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
                            Console.WriteLine($"[ConfigurePhase] Handler returned WAITING, _waitingHandlers before increment: {_waitingHandlers}");
                            _waitingHandlers++;
                            Console.WriteLine($"[ConfigurePhase] _waitingHandlers after increment: {_waitingHandlers}");
                            // Check if Resume() was called during handler execution (pseudo-sequential)
                            // In this case, _waitingHandlers would be decremented by Resume() before we reach this check
                            if (_waitingHandlers <= 0) {
                                Console.WriteLine($"[ConfigurePhase] Pseudo-sequential detected, treating as SUCCESS and continuing");
                                if (IsCancelled) return null; // if the event is already cancelled should not continue processing
                                // Pseudo-sequential case: Resume() was called during handler execution
                                // Treat this handler as successfully completed and continue processing
                                _handlerResults[_currentHandler] = HandlerProcessResult.SUCCESS;
                                // Reset counter and continue to next handler
                                _waitingHandlers = 0;
                                continue;
                            }
                            Console.WriteLine($"[ConfigurePhase] Entering WAITING state");
                            PhaseResult = PhaseProcessResult.WAITING;
                            _stateContext.StateResult = StateProcessResult.WAITING;
                            return this;
                        case HandlerProcessResult.CANCELLED:
                            return Cancel();
                    }
                }
                //StateResult = StateProcessResult.CONTINUE;
                //all handlers failed, skip to cleanup phase
                if (_handlerResults.Count > 0 && _handlerResults.All(x => x.Value == HandlerProcessResult.FAILURE)) {
                    PhaseResult = PhaseProcessResult.FAILURE;
                    return _stateContext.getPhaseState<CleanupPhaseState>();
                }
                PhaseResult = PhaseProcessResult.CONTINUE;
            }
            return _stateContext.getPhaseState<ExecutePhaseState>();
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
            Console.WriteLine($"[ConfigurePhase] Resume() called, _waitingHandlers before decrement: {_waitingHandlers}");
            if (IsCancelled) return Cancel(); //if the event is already cancelled, can't resume
            _waitingHandlers--;
            Console.WriteLine($"[ConfigurePhase] _waitingHandlers after decrement: {_waitingHandlers}");
            // if the counter is zero, continue processing
            if (_waitingHandlers == 0) {
                Console.WriteLine($"[ConfigurePhase] All handlers resumed, continuing processing");
                // tecnically _currentHandler should never be null here, it means that the all handlers have already been processed.
                // but even in this case we should just continue processing since having WAITING status in _handlerResults will not affect the outcome of the phase
                if (_currentHandler != null) // since this should not be possible maybe should throw an exception instead?
                    _handlerResults[_currentHandler!] = HandlerProcessResult.SUCCESS;
                return Process();
            }
            Console.WriteLine($"[ConfigurePhase] Still waiting for more handlers: {_waitingHandlers}");
            //otherwise, there are still handlers waiting
            PhaseResult = PhaseProcessResult.WAITING;
            //there are still handlers waiting, continue in configure phase
            return this;
        }

        public override PhaseState? Fail() {
            if (IsCancelled) return Cancel(); //if the event is already cancelled, can't resume
            _waitingHandlers--;
            // if the counter is zero, continue processing
            if (_waitingHandlers == 0) {
                // tecnically _currentHandler should never be null here, it means that the all handlers have already been processed.
                // but even in this case we should just continue processing since having WAITING status in _handlerResults will not affect the outcome of the phase
                if (_currentHandler != null) // since this should not be possible maybe should throw an exception instead?
                    _handlerResults[_currentHandler!] = HandlerProcessResult.FAILURE;
                return Process();
            }
            //otherwise, there are still handlers waiting
            PhaseResult = PhaseProcessResult.WAITING;
            //there are still handlers waiting, continue in configure phase
            return this;

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
            _stateContext.StateResult = StateProcessResult.CANCELLED;
            return _stateContext.getPhaseState<CleanupPhaseState>();
        }
    }
    #endregion
}
