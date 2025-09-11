using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Business state implementation.
/// 
/// The BusinessState is the primary processing state in the LSEventSystem, responsible for
/// managing the sequential execution of business phases: VALIDATE → CONFIGURE → EXECUTE → CLEANUP.
/// This state implements the core business logic flow and handles the majority of event processing.
/// 
/// Key Responsibilities:
/// - Sequential phase execution with proper flow control
/// - Phase result evaluation and state transition decisions
/// - Handler coordination across multiple phases
/// - Waiting state management for asynchronous operations
/// - Error handling and failure recovery coordination
/// 
/// Phase Execution Model:
/// 1. **Validate Phase**: validation logic, all handlers must be successful to continue
/// 2. **Configure Phase**: Resource allocation, setup, state preparation, failure in all handlers skip Execute phase
/// 3. **Execute Phase**: Core business logic execution
/// 4. **Cleanup Phase**: Resource cleanup, finalization, logging
/// 
/// State Transitions:
/// - Success: All phases complete successfully → SucceedState
/// - Failure: Phases complete with failures → CompletedState
/// - Cancellation: Critical failure during processing → CancelledState
/// - Waiting: External input required → Remains in BusinessState
/// 
/// The BusinessState coordinates with individual PhaseState implementations to ensure
/// proper execution order, result handling, and state machine flow control.
/// </summary>
public partial class LSEventBusinessState : IEventProcessState {
    /// <summary>
    /// Reference to the event system context providing access to event data,
    /// dispatcher, and other processing components.
    /// </summary>
    protected LSEventProcessContext _eventContext;
    
    /// <summary>
    /// The currently active phase being processed.
    /// Null when all phases have completed or processing has terminated.
    /// </summary>
    PhaseState? _currentPhase = null;
    
    /// <summary>
    /// Complete list of all phases in execution order.
    /// Initialized with ValidatePhaseState, ConfigurePhaseState, ExecutePhaseState, CleanupPhaseState.
    /// </summary>
    List<PhaseState> _phases = new();
    
    /// <summary>
    /// Dictionary tracking the completion result of each phase.
    /// Used to evaluate overall business state outcome and determine final transitions.
    /// </summary>
    protected Dictionary<PhaseState, PhaseProcessResult?> _phaseResults = new();
    
    /// <summary>
    /// The final result of business state processing.
    /// Determines the next state transition when business processing completes.
    /// </summary>
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    
    /// <summary>
    /// Indicates whether any phase has encountered failures during processing.
    /// True if any phase result is FAILURE, affecting final state transition logic.
    /// </summary>
    public bool HasFailures => _phaseResults.Values.Any(r => r == PhaseProcessResult.FAILURE);
    
    /// <summary>
    /// Indicates whether any phase has been cancelled during processing.
    /// True if any phase result is CANCELLED, typically leading to immediate termination.
    /// </summary>
    public bool HasCancelled => _phaseResults.Values.Any(r => r == PhaseProcessResult.CANCELLED);
    /// <summary>
    /// Initializes a new BusinessState with the specified context.
    /// 
    /// Construction Process:
    /// 1. Stores the event system context for access to event data and handlers
    /// 2. Filters handlers to include only PhaseHandlerEntry instances
    /// 3. Creates all four phase states in execution order
    /// 4. Sets ValidatePhaseState as the starting phase
    /// 
    /// The phase states are created with the filtered handler collection,
    /// allowing each phase to select and execute only relevant handlers
    /// based on their PhaseType property.
    /// </summary>
    /// <param name="context">The event system context containing event data and handlers</param>
    public LSEventBusinessState(LSEventProcessContext context) {
        _eventContext = context;
        //handlers are ordered and selected by type in the phase itself
        var handlers = _eventContext.Handlers.OfType<LSPhaseHandlerEntry>().ToList();
        var validate = new ValidatePhaseState(this, handlers);
        _phases.Add(validate);
        _phases.Add(new ConfigurePhaseState(this, handlers));
        _phases.Add(new ExecutePhaseState(this, handlers));
        _phases.Add(new CleanupPhaseState(this, handlers));
        _currentPhase = validate;
    }

    /// <summary>
    /// Retrieves a phase state instance by its type.
    /// Used for direct access to specific phases during processing or debugging.
    /// </summary>
    /// <param name="type">The exact type of the phase state to retrieve</param>
    /// <returns>The phase state instance, or null if not found</returns>
    internal PhaseState? getPhaseState(System.Type type) {
        return _phases.FirstOrDefault(p => p.GetType() == type);
    }
    
    /// <summary>
    /// Retrieves a phase state instance by its generic type parameter.
    /// Type-safe access to specific phase states with compile-time type checking.
    /// </summary>
    /// <typeparam name="T">The specific phase state type to retrieve</typeparam>
    /// <returns>The phase state instance cast to the specified type, or null if not found</returns>
    internal T? getPhaseState<T>() where T : PhaseState {
        return getPhaseState(typeof(T)) as T;
    }
    
    /// <summary>
    /// Attempts to retrieve a phase state instance with safe null checking.
    /// Provides the TryGet pattern for safe access to phase states.
    /// </summary>
    /// <typeparam name="T">The specific phase state type to retrieve</typeparam>
    /// <param name="phaseState">When this method returns, contains the phase state if found</param>
    /// <returns>true if the phase state was found; otherwise, false</returns>
    internal bool tryGetPhaseState<T>(out T? phaseState) where T : PhaseState {
        phaseState = getPhaseState<T>();
        return phaseState != null;
    }
    /// <summary>
    /// Core phase processing logic shared by both Process() and Resume() operations.
    /// 
    /// Handles the common flow for phase execution:
    /// 1. Checks if current phase exists (returns SucceedState if none)
    /// 2. Executes the provided callback (Process or Resume)
    /// 3. Evaluates the phase result and updates tracking
    /// 4. Determines state transitions based on phase outcome
    /// 5. Updates current phase and returns appropriate state
    /// 
    /// Phase Result Handling:
    /// - CANCELLED: Immediate transition to CancelledState
    /// - FAILURE: Continue to next phase or transition to CompletedState if last
    /// - WAITING: Remain in current state for external input
    /// - CONTINUE: Proceed to next phase in sequence
    /// 
    /// This method centralizes the phase transition logic to ensure consistent
    /// behavior between normal processing and resumption scenarios.
    /// </summary>
    /// <param name="callback">Function to execute on the current phase (Process or Resume)</param>
    /// <returns>The next state to transition to based on phase results</returns>
    IEventProcessState phaseProcess(Func<PhaseState?> callback) {
        if (_currentPhase == null) return new LSEventSucceedState(_eventContext); //no phases left to process
        var nextPhase = callback();
        var processPhaseResult = _currentPhase.PhaseResult;
        _phaseResults[_currentPhase] = processPhaseResult;
        switch (processPhaseResult) {
            case PhaseProcessResult.CANCELLED: 
                // Special handling for CleanupPhase cancellation
                if (_currentPhase is CleanupPhaseState) {
                    // CleanupPhase cancellation should still result in SUCCESS
                    // since core business phases (Validate, Configure, Execute) completed successfully
                    StateResult = StateProcessResult.SUCCESS;
                    _currentPhase = null;
                    return new LSEventSucceedState(_eventContext);
                } else {
                    // Other phase cancellations move to cancelled state
                    StateResult = StateProcessResult.CANCELLED;
                    _currentPhase = null;
                    return new LSEventCancelledState(_eventContext);
                }
            case PhaseProcessResult.FAILURE: //when phase fails move to completed state
                if (nextPhase == null) {
                    StateResult = StateProcessResult.FAILURE;
                    _currentPhase = null;
                    return new LSEventCompletedState(_eventContext);
                }
                break;
            case PhaseProcessResult.WAITING: //this can happen when is the last phase but a handler is still waiting
                StateResult = StateProcessResult.WAITING;
                return this;
        }
        _currentPhase = nextPhase;
        return this;
    }

    /// <summary>
    /// Processes the business state through all phases until completion or waiting.
    /// 
    /// Execution Flow:
    /// 1. Checks if there are phases to process
    /// 2. Executes phases sequentially via phaseProcess()
    /// 3. Continues until all phases complete or processing pauses
    /// 4. Returns the appropriate next state based on results
    /// 
    /// Phase Sequence:
    /// VALIDATE → CONFIGURE → EXECUTE → CLEANUP → SucceedState/CompletedState
    /// 
    /// Early Termination:
    /// - CANCELLED result: Immediate transition to CancelledState
    /// - WAITING result: Remains in BusinessState for external input
    /// - FAILURE result: Continues to cleanup, then CompletedState
    /// 
    /// Success Path:
    /// All phases complete successfully → SucceedState → CompletedState
    /// </summary>
    /// <returns>
    /// The next state in the processing pipeline:
    /// - SucceedState: All phases completed successfully
    /// - CompletedState: Phases completed with failures
    /// - CancelledState: Critical failure during processing  
    /// - BusinessState: Waiting for external input
    /// </returns>
    public IEventProcessState Process() {
        IEventProcessState result = this;
        if (_currentPhase == null) return new LSEventSucceedState(_eventContext); //all phases completed successfully move to succeed state
        do {
            result = phaseProcess(_currentPhase.Process);
        } while (_currentPhase != null);

        return result; //all phases completed successfully move to succeed state
    }
    /// <summary>
    /// Resumes processing from a waiting state.
    /// 
    /// Called when external operations complete and processing should continue
    /// from where it was paused. Uses the same core logic as Process() but
    /// calls Resume() on the current phase instead of Process().
    /// 
    /// Resume Scenarios:
    /// - Async operation completed successfully
    /// - External service became available
    /// - User input was provided
    /// - Timeout handling with continuation
    /// 
    /// The method continues processing from the current phase and may execute
    /// subsequent phases if the resumed phase completes successfully.
    /// 
    /// Error Handling:
    /// - Throws LSException if no active phase to resume
    /// - Handles phase results same as Process() method
    /// - May transition to failure or cancellation states
    /// </summary>
    /// <returns>
    /// The next state after resumption:
    /// - SucceedState: Resumed processing completed successfully
    /// - CompletedState: Resumed processing completed with failures
    /// - CancelledState: Resumed processing was cancelled
    /// - BusinessState: Still waiting for additional external input
    /// </returns>
    /// <exception cref="LSException">Thrown when there is no active phase to resume</exception>
    public IEventProcessState Resume() {
        IEventProcessState result = this;
        if (_currentPhase == null) throw new LSException("Cannot resume business state with no active phase.");
        do {
            result = phaseProcess(_currentPhase.Resume);
        } while (_currentPhase != null);

        return result; //all phases completed successfully move to succeed state
    }
    /// <summary>
    /// Cancels the current business state processing.
    /// 
    /// Triggers immediate termination of business processing and transition
    /// to CancelledState for cleanup and finalization. The current phase
    /// is given an opportunity to handle cancellation through its Cancel() method.
    /// 
    /// Cancellation Scenarios:
    /// - External cancellation request
    /// - Critical failure requiring immediate termination
    /// - User-initiated cancellation
    /// - System shutdown or timeout conditions
    /// 
    /// The method ensures proper cleanup by transitioning to CancelledState
    /// regardless of the current phase's cancellation handling result.
    /// </summary>
    /// <returns>
    /// Always returns CancelledState for proper cleanup and finalization.
    /// </returns>
    public IEventProcessState Cancel() {
        var result = _currentPhase?.Cancel();
        return new LSEventCancelledState(_eventContext);
    }
    
    /// <summary>
    /// Handles failure scenarios in the business state.
    /// 
    /// Currently returns the same state to continue processing, allowing
    /// individual phases to handle their own failure logic. This method
    /// provides a hook for future failure handling enhancements.
    /// 
    /// Failure vs Cancellation:
    /// - Fail(): Recoverable errors, processing can continue
    /// - Cancel(): Critical errors, immediate termination required
    /// </summary>
    /// <returns>
    /// Returns this BusinessState to continue processing with failure handling.
    /// </returns>
    public IEventProcessState Fail() {
        return this;
    }
}
