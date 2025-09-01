namespace LSUtils.EventSystem;

/// <summary>
/// Delegate that defines the signature for phase handlers in the event processing pipeline.
/// Phase handlers are responsible for processing events during specific phases and should
/// follow functional programming principles to maintain system predictability.
/// </summary>
/// <typeparam name="TEvent">The specific type of event this handler can process. Must implement ILSEvent.</typeparam>
/// <param name="event">The event instance being processed. Contains all event data and state information.</param>
/// <param name="context">
/// Execution context providing information about the current processing state,
/// including the current phase, elapsed time, error information, and execution metrics.
/// This context is read-only and should be used for observability and decision-making.
/// </param>
/// <returns>
/// An LSPhaseResult value indicating how event processing should continue:
/// - CONTINUE: Process the next handler in the current phase
/// - CANCEL: Stop all processing and mark the event as cancelled
/// - SKIP_REMAINING: Skip remaining handlers in the current phase and move to the next phase
/// - RETRY: Request retry of the current handler execution
/// - WAITING: Pause processing until Resume(), Abort(), or Fail() is called
/// </returns>
public delegate LSHandlerResult LSPhaseHandler<in TEvent>(TEvent @event, LSPhaseContext context) where TEvent : ILSEvent;
