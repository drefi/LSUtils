using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Context for state transitions and phase execution in v4.
/// 
/// The EventSystemContext serves as the central coordination point for event processing,
/// managing the state machine lifecycle and providing access to all processing components.
/// 
/// Key Responsibilities:
/// - State machine coordination and transitions
/// - Handler management and execution context
/// - Event lifecycle tracking (failures, cancellation, completion)
/// - Dispatcher integration for handler retrieval and execution
/// 
/// State Machine Flow:
/// 1. Initialization: Creates BusinessState as starting state
/// 2. Processing: Executes state.Process() until completion or waiting
/// 3. Transitions: Manages state changes based on processing results
/// 4. Termination: Handles final state transitions (Success, Failure, Cancelled)
/// 
/// Thread Safety:
/// - State transitions are managed internally by individual state implementations
/// - Context properties are read-only after initialization
/// - Handler execution follows phase-based sequential model
/// 
/// Usage Pattern:
/// The context is created internally by BaseEvent.Dispatch() and should not be
/// instantiated directly by client code. External actors can interact with
/// waiting states through Resume(), Cancel(), and Fail() methods.
/// </summary>
public class LSEventProcessContext {
    /// <summary>
    /// The dispatcher instance used for handler retrieval and management.
    /// Provides access to globally registered handlers for event processing.
    /// </summary>
    public LSDispatcher Dispatcher { get; }
    
    /// <summary>
    /// The current state in the event processing state machine.
    /// Can be BusinessState, SucceedState, CancelledState, or CompletedState.
    /// Null when processing is complete.
    /// </summary>
    public IEventProcessState? CurrentState { get; internal set; }
    
    /// <summary>
    /// The event being processed through the state machine.
    /// Contains all event data and provides access to event metadata.
    /// </summary>
    public ILSEvent Event { get; internal set; }
    
    /// <summary>
    /// Read-only collection of all handlers available for this event.
    /// Includes both global handlers from the dispatcher and event-scoped handlers.
    /// </summary>
    public IReadOnlyList<IHandlerEntry> Handlers { get; }

    /// <summary>
    /// Indicates if the event processing has encountered failures.
    /// True when any handler has failed or returned failure results.
    /// Does not prevent processing completion - events can complete with failures.
    /// </summary>
    public bool HasFailures { get; internal set; }
    
    /// <summary>
    /// Indicates if the event processing has been cancelled.
    /// True when cancellation has been requested or a handler returned CANCELLED.
    /// Results in immediate termination of processing and transition to CancelledState.
    /// </summary>
    public bool IsCancelled { get; internal set; }

    /// <summary>
    /// Initializes a new EventSystemContext with the specified components.
    /// 
    /// The context is created with:
    /// - BusinessState as the initial state for phase-based processing
    /// - Handler collection from both global dispatcher and event-scoped sources
    /// - Event reference for state machine processing
    /// 
    /// This constructor is internal and should only be called by the Create factory method.
    /// </summary>
    /// <param name="dispatcher">The dispatcher for handler management</param>
    /// <param name="event">The event to process</param>
    /// <param name="handlers">Complete collection of handlers for this event</param>
    protected LSEventProcessContext(LSDispatcher dispatcher, ILSEvent @event, IReadOnlyList<IHandlerEntry> handlers) {
        Event = @event;
        Dispatcher = dispatcher;
        Handlers = handlers;
        CurrentState = new LSEventBusinessState(this);
    }

    /// <summary>
    /// Processes the event through its complete lifecycle until completion, cancellation, or waiting state.
    /// 
    /// Processing Flow:
    /// 1. Validates event is marked as InDispatch
    /// 2. Executes state machine loop until CurrentState is null or WAITING
    /// 3. Processes each state via state.Process() method
    /// 4. Handles state transitions based on StateResult
    /// 5. Updates context flags (HasFailures, IsCancelled) based on results
    /// 6. Returns final processing result
    /// 
    /// State Transition Logic:
    /// - SUCCESS: Continue to next state
    /// - FAILURE: Mark HasFailures, continue processing  
    /// - CANCELLED: Mark IsCancelled, may continue for cleanup
    /// - WAITING: Pause processing, return WAITING result
    /// 
    /// This method can only be called by the event itself and should not be accessed externally.
    /// </summary>
    /// <returns>
    /// Final processing result:
    /// - SUCCESS: Event completed successfully
    /// - FAILURE: Event completed with failures
    /// - CANCELLED: Event was cancelled
    /// - WAITING: Event is waiting for external input
    /// </returns>
    /// <exception cref="LSException">Thrown when event is not marked as InDispatch</exception>
    internal EventProcessResult processEvent() {
        if (Event.InDispatch == false) throw new LSException("Event must be marked as InDispatch before processing.");
        while (CurrentState != null) {
            var nextState = CurrentState.Process();
            var result = CurrentState.StateResult;
            switch (result) {
                case StateProcessResult.CANCELLED:
                    IsCancelled = true;
                    break;
                case StateProcessResult.FAILURE:
                    HasFailures = true;
                    break;
                case StateProcessResult.WAITING:
                    //stay in current state
                    return EventProcessResult.WAITING;
                case StateProcessResult.SUCCESS:
                default:
                    break;
            }
            CurrentState = nextState;
        }
        return IsCancelled ? EventProcessResult.CANCELLED : HasFailures ? EventProcessResult.FAILURE : EventProcessResult.SUCCESS;
    }

    /// <summary>
    /// Resumes processing from a waiting state.
    /// 
    /// Called by external actors when async operations complete successfully.
    /// Delegates to the current state's Resume() method to continue processing
    /// from where it was paused.
    /// 
    /// Typical usage scenario:
    /// 1. Handler returns WAITING result
    /// 2. Event processing pauses
    /// 3. External operation completes
    /// 4. External actor calls Resume()
    /// 5. Processing continues from the paused state
    /// </summary>
    /// <returns>The next state to transition to, or null if processing remains in current state</returns>
    public IEventProcessState? Resume() => CurrentState?.Resume();

    /// <summary>
    /// Cancels processing from a waiting state.
    /// 
    /// Called by external actors when async operations are cancelled or 
    /// when a critical failure requires immediate termination.
    /// Delegates to the current state's Cancel() method.
    /// 
    /// Results in transition to CancelledState and eventual completion.
    /// </summary>
    /// <returns>The next state to transition to, typically CancelledState</returns>
    public IEventProcessState? Cancel() => CurrentState?.Cancel();
    
    /// <summary>
    /// Marks processing as failed from a waiting state.
    /// 
    /// Called by external actors when async operations fail but processing
    /// should continue through failure handling phases.
    /// Delegates to the current state's Fail() method.
    /// 
    /// Sets HasFailures flag and continues processing through appropriate phases.
    /// </summary>
    /// <returns>The next state to transition to, allowing failure handling to continue</returns>
    public IEventProcessState? Fail() => CurrentState?.Fail();

    /// <summary>
    /// Factory method for creating EventSystemContext instances.
    /// 
    /// Provides controlled instantiation with proper initialization of all components.
    /// This is the only way to create context instances, ensuring consistent setup.
    /// </summary>
    /// <param name="dispatcher">The dispatcher for handler management</param>
    /// <param name="event">The event to process through the state machine</param>
    /// <param name="handlers">Complete handler collection for this event</param>
    /// <returns>A fully initialized EventSystemContext ready for processing</returns>
    internal static LSEventProcessContext Create(LSDispatcher dispatcher, ILSEvent @event, IReadOnlyList<IHandlerEntry> handlers) {
        return new LSEventProcessContext(dispatcher, @event, handlers);
    }
}
