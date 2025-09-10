using System.Collections.Generic;

namespace LSUtils.EventSystem;

public partial class BusinessState {
    #region Phase State
    /// <summary>
    /// First phase in the business processing pipeline responsible for validation and early checks.
    /// 
    /// The ValidatePhaseState represents the initial phase of business processing where
    /// input validation, security checks, and early validation logic are performed.
    /// This phase has strict failure handling - any failure immediately terminates
    /// business processing.
    /// 
    /// Key Characteristics:
    /// - First phase in business processing pipeline
    /// - Strict failure handling - any failure terminates processing
    /// - Waiting states cause validation failure (must be synchronous)
    /// - Single failure skips remaining handlers and ends processing
    /// - Critical for ensuring data integrity and security
    /// 
    /// Handler Execution Model:
    /// - Sequential handler execution in priority order
    /// - First failure terminates entire phase and processing
    /// - All handlers must complete synchronously (no waiting)
    /// - Cancellation requests terminate processing immediately
    /// 
    /// Validation Responsibilities:
    /// - Input data format and structure validation
    /// - Business rule validation and constraint checking
    /// - Security checks and permission validation
    /// - Authentication and authorization verification
    /// - Data consistency and integrity checks
    /// - Early error detection and prevention
    /// 
    /// Failure Scenarios:
    /// - FAILURE: Immediate termination, skip remaining handlers and phases
    /// - CANCELLED: Immediate termination with cancellation status
    /// - WAITING: Treated as validation failure (async not allowed)
    /// - SUCCESS: Continue to next handler or next phase
    /// 
    /// Design Philosophy:
    /// The validate phase follows a "fail fast" approach where any validation
    /// failure immediately stops processing. This prevents invalid data from
    /// progressing through the system and ensures early error detection.
    /// 
    /// State Transitions:
    /// - Success: Transition to ConfigurePhaseState
    /// - Failure: Terminate business processing (no further phases)
    /// - Cancelled: Terminate with cancellation status
    /// - Waiting: Treated as failure (validation must be synchronous)
    /// </summary>
    public class ValidatePhaseState : PhaseState {
        /// <summary>
        /// Constructs a new ValidatePhaseState with the specified context and handlers.
        /// 
        /// Initializes the validation phase with handlers responsible for early
        /// validation, security checks, and input verification before business
        /// logic execution begins.
        /// </summary>
        /// <param name="context">The BusinessState context managing phase transitions</param>
        /// <param name="handlers">Collection of validation handlers to execute</param>
        public ValidatePhaseState(BusinessState context, List<LSPhaseHandlerEntry> handlers) : base(context, handlers) { }

        /// <summary>
        /// Processes the validation phase with strict failure handling.
        /// 
        /// The validation phase employs a "fail fast" strategy where any handler
        /// failure immediately terminates processing. This ensures that invalid
        /// data or failed security checks prevent further processing.
        /// 
        /// Execution Flow:
        /// 1. Execute handlers sequentially in priority order
        /// 2. On SUCCESS: Continue to next handler
        /// 3. On FAILURE: Set failure status and terminate immediately
        /// 4. On WAITING: Continue execution, check at end
        /// 5. On CANCELLED: Terminate with cancellation status
        /// 6. Post-processing: Check for waiting handlers (causes failure)
        /// 7. Success: Transition to ConfigurePhaseState
        /// 
        /// Handler Result Processing:
        /// - SUCCESS: Normal continuation to next handler
        /// - FAILURE: Immediate phase failure, skip remaining handlers, end state
        /// - WAITING: Continue processing, but fail if any waiting at end
        /// - CANCELLED: Immediate cancellation and termination
        /// 
        /// Waiting Handler Policy:
        /// Validation phase requires synchronous operation. Any handlers that
        /// return WAITING are allowed to execute, but their presence causes
        /// the entire phase to fail after all handlers complete. This ensures
        /// validation remains fast and predictable.
        /// 
        /// Validation Examples:
        /// - Data format validation (email, phone, dates)
        /// - Business rule validation (age limits, quotas)
        /// - Security validation (permissions, authentication)
        /// - Input sanitization and bounds checking
        /// - Reference data validation (foreign keys)
        /// 
        /// Critical Design:
        /// The strict failure handling ensures that no invalid data progresses
        /// through the system. This is essential for data integrity, security,
        /// and preventing downstream errors that are harder to diagnose.
        /// </summary>
        /// <returns>
        /// ConfigurePhaseState if validation succeeds, null if validation fails or is cancelled
        /// </returns>
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
        
        /// <summary>
        /// Handles cancellation requests during validation processing.
        /// 
        /// When cancellation is requested during validation, the phase is marked
        /// as cancelled and processing terminates immediately. This typically
        /// occurs due to security violations or critical validation failures.
        /// 
        /// Cancellation Effects:
        /// - Sets phase result to CANCELLED
        /// - Sets business state result to CANCELLED
        /// - Terminates phase processing immediately
        /// - Prevents transition to subsequent phases
        /// 
        /// Common Cancellation Scenarios:
        /// - Security violations detected
        /// - Authentication failures
        /// - Authorization denied
        /// - Critical data corruption detected
        /// - System-level validation failures
        /// </summary>
        /// <returns>Always null to indicate immediate processing termination</returns>
        public override PhaseState? Cancel() {
            PhaseResult = PhaseProcessResult.CANCELLED;
            _context.StateResult = StateProcessResult.CANCELLED; //this is a drible da vaca
            return null;
        }
    }
    #endregion
}
