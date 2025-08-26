namespace LSUtils.EventSystem;

/// <summary>
/// Event-scoped callback builder that provides a fluent API for registering one-time handlers
/// specific to a particular event instance. This builder automatically manages handler lifecycle
/// and cleanup, making it ideal for event-specific processing logic.
/// 
/// The callback builder provides the following methods:
/// 
/// **Core Phase Methods:**
/// - OnValidation, OnPrepare, OnExecution, OnSuccess, OnComplete (with optional priority)
/// 
/// **State-Based Handlers:**
/// - OnError(action) - simple error cleanup actions (single parameter: evt)
/// - OnSuccess(action) - simple success actions (single parameter: evt)
/// - OnCancel(action) - simple cancellation cleanup actions (single parameter: evt)
/// </summary>
/// <remarks>
/// Key Features:
/// - **Instance Isolation**: Handlers only execute for the specific event instance
/// - **Automatic Cleanup**: All registered handlers are automatically cleaned up after processing
/// - **One-time Execution**: Handlers are limited to single execution by default
/// - **Type Safety**: Full compile-time type checking for event types
/// - **Fluent API**: Method chaining for readable configuration
/// - **Error Management**: Built-in error handling with data-based error storage
/// 
/// Usage Patterns:
/// <code>
/// // Simple inline processing
/// var success = myEvent.Build&lt;MyEvent&gt;(dispatcher)
///     .OnValidation((evt, ctx) => ValidateEvent(evt))
///     .OnExecution((evt, ctx) => ProcessEvent(evt))
///     .OnCancel(evt => CleanupOnCancel(evt))
///     .Dispatch();
/// 
/// // With priorities
/// var success = myEvent.Build&lt;MyEvent&gt;(dispatcher)
///     .OnValidation(CriticalValidation, LSPhasePriority.CRITICAL)
///     .OnExecution(MainProcessing, LSPhasePriority.NORMAL)
///     .Dispatch();
/// </code>
/// </remarks>
///     .OnTimeout(TimeSpan.FromSeconds(30), (evt, ctx) => HandleTimeout(evt))
///     .TransformData&lt;string&gt;("user.name", name => name.Trim().ToLower())
///     .OnSuccess(evt => SendConfirmationEmail(evt))
///     .OnError(evt => LogError(evt))
///     .Dispatch();
/// 
/// // Manual lifecycle management
/// var builder = myEvent.Build&lt;MyEvent&gt;(dispatcher);
/// builder.OnValidation(ValidateHandler)
///        .OnExecution(ExecuteHandler);
/// var success = builder.Dispatch();
/// </code>
/// </remarks>
/// <typeparam name="TEvent">The event type this builder is configured for.</typeparam>
public class LSEventCallbackBuilder<TEvent> where TEvent : ILSEvent {
    private readonly TEvent _event;
    private readonly LSDispatcher _dispatcher;
    private readonly LSEventCallbackBatch<TEvent> _batch;
    private bool _isRegistered = false;
    
    /// <summary>
    /// Initializes a new callback builder for the specified event and dispatcher.
    /// </summary>
    /// <param name="event">The event instance this builder is bound to.</param>
    /// <param name="dispatcher">The dispatcher to register handlers with.</param>
    internal LSEventCallbackBuilder(TEvent @event, LSDispatcher dispatcher) {
        _event = @event;
        _dispatcher = dispatcher;
        _batch = new LSEventCallbackBatch<TEvent>(@event);
    }
    
    #region Core Phase Methods
    
    /// <summary>
    /// Registers a handler for the VALIDATE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during validation.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnValidation(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.VALIDATE, priority);
    }
    
    /// <summary>
    /// Registers a handler for the PREPARE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during preparation.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnPrepare(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.PREPARE, priority);
    }
    
    /// <summary>
    /// Registers a handler for the EXECUTE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during execution.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnExecution(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.EXECUTE, priority);
    }
    
    /// <summary>
    /// Registers a handler for the COMPLETE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during completion.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnComplete(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.COMPLETE, priority);
    }
    
    #endregion
    
    #region Conditional and State-Based Methods
    
    /// <summary>
    /// Registers a simple action that executes when the event has an error.
    /// This runs during the COMPLETE phase only when the event has an error message.
    /// </summary>
    /// <param name="action">Action to execute on error</param>
    /// <returns>This builder for fluent chaining</returns>
    public LSEventCallbackBuilder<TEvent> OnError(LSAction<TEvent> action) {
        return RegisterEventSpecificHandler(
            (evt, ctx) => {
                action(evt);
                return LSPhaseResult.CONTINUE;
            }, 
            LSEventPhase.COMPLETE, 
            LSPhasePriority.NORMAL,
            evt => !string.IsNullOrEmpty(GetErrorMessage(evt))
        );
    }
    
    /// <summary>
    /// Registers a simple action that executes during the CANCEL phase when the event is cancelled.
    /// For complex cancellation logic with conditions or phase-specific handling, use OnComplete
    /// with your own conditional logic.
    /// </summary>
    /// <param name="action">The action to execute on cancellation</param>
    /// <returns>This builder for fluent chaining</returns>
    public LSEventCallbackBuilder<TEvent> OnCancel(LSAction<TEvent> action) {
        return RegisterEventSpecificHandler(
            (evt, _) => {
                action(evt);
                return LSPhaseResult.CONTINUE;
            }, 
            LSEventPhase.CANCEL, 
            LSPhasePriority.NORMAL
        );
    }
    
    /// <summary>
    /// Registers a simple action that executes when the event completes successfully.
    /// </summary>
    /// <param name="action">Action to execute on success</param>
    /// <returns>This builder for fluent chaining</returns>
    public LSEventCallbackBuilder<TEvent> OnSuccess(LSAction<TEvent> action) {
        return RegisterEventSpecificHandler(
            (evt, ctx) => {
                action(evt);
                return LSPhaseResult.CONTINUE;
            }, 
            LSEventPhase.SUCCESS, 
            LSPhasePriority.NORMAL,
            null
        );
    }
    
    #endregion
    
    #region Monitoring Methods
    
    /// <summary>
    /// Measures and reports execution time.
    /// </summary>
    /// <param name="callback">The callback to receive timing information.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> MeasureExecutionTime(System.Action<TEvent, System.TimeSpan> callback) {
        var startTime = System.DateTime.UtcNow;
        return OnComplete((evt, ctx) => {
            var elapsed = System.DateTime.UtcNow - startTime;
            callback(evt, elapsed);
            return LSPhaseResult.CONTINUE;
        });
    }
    
    #endregion
    
    #region Error Handling Methods
    
    /// <summary>
    /// Cancels the event if a condition is met during the specified phase.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="errorMessage">The error message to set.</param>
    /// <param name="phase">The phase in which to check the condition (default: VALIDATE).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> CancelIf(LSEventCondition<TEvent> condition, string errorMessage = "", LSEventPhase phase = LSEventPhase.VALIDATE) {
        return RegisterEventSpecificHandler((evt, ctx) => {
            if (condition(evt)) {
                if (!string.IsNullOrEmpty(errorMessage)) {
                    SetErrorMessage(evt, errorMessage);
                }
                return LSPhaseResult.CANCEL;
            }
            return LSPhaseResult.CONTINUE;
        }, phase);
    }
    
    /// <summary>
    /// Retries a handler on error up to the maximum retry count.
    /// </summary>
    /// <param name="handler">The handler to retry.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> RetryOnError(LSPhaseHandler<TEvent> handler, int maxRetries = 3) {
        return RegisterEventSpecificHandler((evt, ctx) => {
            try {
                return handler(evt, ctx);
            } catch (LSException ex) {
                var retryKey = $"retry.attempt.{ctx.CurrentPhase}";
                var currentRetries = evt.TryGetData<int>(retryKey, out var count) ? count : 0;
                
                if (currentRetries < maxRetries) {
                    evt.SetData(retryKey, currentRetries + 1);
                    evt.SetData($"retry.error.{currentRetries}", ex.Message);
                    return LSPhaseResult.RETRY;
                }
                
                SetErrorMessage(evt, $"Handler failed after {maxRetries} retries: {ex.Message}");
                return LSPhaseResult.CANCEL;
            }
        }, LSEventPhase.EXECUTE);
    }
    
    #endregion
    
    #region Processing
    
    /// <summary>
    /// Dispatches the event with registered handlers and automatically cleans up.
    /// </summary>
    /// <returns>True if the event completed successfully, false if it was cancelled or had errors.</returns>
    public bool Dispatch() {
        try {
            // Register the batch if not already registered
            if (!_isRegistered) {
                _dispatcher.RegisterBatchedHandlers(_batch);
                _isRegistered = true;
            }
            
            return _dispatcher.ProcessEvent(_event);
        } finally {
            // Auto-cleanup: the batch handler will automatically clean up after one execution
            // No manual cleanup needed due to MaxExecutions = 1 in RegisterBatchedHandlers
        }
    }

    #endregion
    
    #region Private Helper Methods
    
    private LSEventCallbackBuilder<TEvent> RegisterEventSpecificHandler(
        LSPhaseHandler<TEvent> handler, 
        LSEventPhase phase, 
        LSPhasePriority priority = LSPhasePriority.NORMAL,
        LSEventCondition<TEvent>? condition = null) {
        
        // Add handler to batch instead of registering immediately
        switch (phase) {
            case LSEventPhase.VALIDATE:
                _batch.OnValidation(handler, priority, condition);
                break;
            case LSEventPhase.PREPARE:
                _batch.OnPrepare(handler, priority, condition);
                break;
            case LSEventPhase.EXECUTE:
                _batch.OnExecution(handler, priority, condition);
                break;
            case LSEventPhase.SUCCESS:
                _batch.OnSuccess(handler, priority, condition);
                break;
            case LSEventPhase.CANCEL:
                _batch.OnCancel(handler, priority, condition);
                break;
            case LSEventPhase.COMPLETE:
                _batch.OnComplete(handler, priority, condition);
                break;
        }
        
        return this;
    }
    
    /// <summary>
    /// Helper method to get error message from an event.
    /// </summary>
    /// <param name="evt">The event to get error message from.</param>
    /// <returns>The error message or null if none exists.</returns>
    private static string? GetErrorMessage(TEvent evt) {
        if (evt is LSBaseEvent baseEvent) {
            return baseEvent.ErrorMessage;
        }
        
        // Fallback to data dictionary for non-LSBaseEvent implementations
        if (evt.TryGetData<string>("error.message", out var errorMsg)) {
            return errorMsg;
        }
        return null;
    }
    
    /// <summary>
    /// Helper method to set error message on an event.
    /// </summary>
    /// <param name="evt">The event to set error message on.</param>
    /// <param name="message">The error message to set.</param>
    private static void SetErrorMessage(TEvent evt, string message) {
        if (evt is LSBaseEvent baseEvent) {
            baseEvent.ErrorMessage = message;
        } else {
            // Fallback to data dictionary for non-LSBaseEvent implementations
            evt.SetData("error.message", message);
        }
    }
    
    #endregion
}
