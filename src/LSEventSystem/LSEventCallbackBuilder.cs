using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Internal structure representing a batched handler within an event-scoped callback builder.
/// This allows multiple handlers to be registered as a single unit for better performance.
/// </summary>
public struct LSBatchedHandler<TEvent> where TEvent : ILSEvent {
    /// <summary>
    /// The phase in which this handler should execute.
    /// </summary>
    public LSEventPhase Phase { get; init; }
    
    /// <summary>
    /// The execution priority within the phase.
    /// </summary>
    public LSESPriority Priority { get; init; }
    
    /// <summary>
    /// The handler function to execute.
    /// </summary>
    public LSPhaseHandler<TEvent> Handler { get; init; }
    
    /// <summary>
    /// Optional condition that must be met for this handler to execute.
    /// </summary>
    public LSEventCondition<TEvent>? Condition { get; init; }
}

/// <summary>
/// Event-scoped callback builder that provides a fluent API for registering one-time handlers
/// specific to a particular event instance. This builder automatically manages handler lifecycle
/// and cleanup, making it ideal for event-specific processing logic.
/// 
/// Key Features:
/// - Instance Isolation: Handlers only execute for the specific event instance
/// - Automatic Cleanup: All registered handlers are automatically cleaned up after processing
/// - One-time Execution: Handlers are limited to single execution by default
/// - Type Safety: Full compile-time type checking for event types
/// - Fluent API: Method chaining for readable configuration
/// - Integrated Batch Management: Internally batches handlers for optimal performance
/// 
/// Core Phase Methods: OnValidatePhase, OnPreparePhase, OnExecutePhase, OnSuccessPhase, OnCancelPhase, OnCompletePhase
/// State-Based Handlers: OnError, OnSuccess, OnCancel, OnComplete (simplified single-parameter actions)
/// Conditional Methods: CancelIf, FailIf
/// </summary>
/// <typeparam name="TEvent">The event type this builder is configured for.</typeparam>
public class LSEventCallbackBuilder<TEvent> where TEvent : ILSEvent {
    private readonly TEvent _event;
    public LSDispatcher Dispatcher { get; protected set; }
    private readonly List<LSBatchedHandler<TEvent>> _handlers = new();
    private readonly object _lock = new object();
    private bool _isRegistered = false;
    private bool _isFinalized = false;
    private readonly List<string> _validationWarnings = new();

    /// <summary>
    /// Initializes a new callback builder for the specified event and dispatcher.
    /// </summary>
    /// <param name="event">The event instance this builder is bound to.</param>
    /// <param name="dispatcher">The dispatcher to register handlers with.</param>
    internal LSEventCallbackBuilder(TEvent @event, LSDispatcher dispatcher) {
        _event = @event;
        Dispatcher = dispatcher;
    }
    
    /// <summary>
    /// Gets the target event instance this builder is bound to.
    /// </summary>
    public TEvent TargetEvent => _event;
    
    /// <summary>
    /// Gets the collected handlers in this builder.
    /// </summary>
    public IReadOnlyList<LSBatchedHandler<TEvent>> Handlers {
        get {
            lock (_lock) {
                return _handlers.AsReadOnly();
            }
        }
    }
    
    /// <summary>
    /// Gets whether this builder has been finalized and can no longer accept new handlers.
    /// </summary>
    public bool IsFinalized {
        get {
            lock (_lock) {
                return _isFinalized;
            }
        }
    }
    
    /// <summary>
    /// Gets any validation warnings that were detected during handler registration.
    /// </summary>
    public IReadOnlyList<string> ValidationWarnings => _validationWarnings.AsReadOnly();
    
    /// <summary>
    /// Gets the number of handlers currently registered in this builder.
    /// </summary>
    public int HandlerCount {
        get {
            lock (_lock) {
                return _handlers.Count;
            }
        }
    }
    
    /// <summary>
    /// Finalizes the builder, preventing further handler additions and optimizing internal storage.
    /// </summary>
    internal void finalizeBatch() {
        lock (_lock) {
            if (_isFinalized)
                return;
                
            _isFinalized = true;
        }
    }
    
    /// <summary>
    /// Validates the builder configuration and returns any validation issues.
    /// </summary>
    /// <returns>List of validation warnings or errors, empty if valid.</returns>
    private List<string> validateBatch() {
        var issues = new List<string>();
        
        lock (_lock) {
            if (!_handlers.Any()) {
                issues.Add("Batch contains no handlers - nothing will be executed");
                return issues;
            }
            
            if (_event.IsCancelled) {
                var nonCancelHandlers = _handlers.Count(h => h.Phase != LSEventPhase.CANCEL && h.Phase != LSEventPhase.COMPLETE);
                if (nonCancelHandlers > 0) {
                    issues.Add($"Event is already cancelled, but {nonCancelHandlers} non-cancel handlers are registered");
                }
            }
        }
        
        return issues;
    }
    
    /// <summary>
    /// Adds a handler to the builder for the specified phase.
    /// </summary>
    /// <param name="handler">The handler to add.</param>
    /// <param name="phase">The phase to execute in.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <param name="condition">Optional condition for execution.</param>
    private void addHandler(LSPhaseHandler<TEvent> handler, LSEventPhase phase, LSESPriority priority = LSESPriority.NORMAL, LSEventCondition<TEvent>? condition = null) {
        lock (_lock) {
            if (_isFinalized) {
                throw new InvalidOperationException("Cannot add handlers to a finalized builder");
            }
            
            _handlers.Add(new LSBatchedHandler<TEvent> {
                Phase = phase,
                Priority = priority,
                Handler = handler,
                Condition = condition
            });
        }
    }

    #region Core Phase Methods

    /// <summary>
    /// Registers a handler for the VALIDATE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during validation.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnValidatePhase(LSPhaseHandler<TEvent> handler, LSESPriority priority = LSESPriority.NORMAL) {
        return registerEventSpecificHandler(handler, LSEventPhase.VALIDATE, priority);
    }

    /// <summary>
    /// Registers a handler for the PREPARE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during preparation.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnPreparePhase(LSPhaseHandler<TEvent> handler, LSESPriority priority = LSESPriority.NORMAL) {
        return registerEventSpecificHandler(handler, LSEventPhase.PREPARE, priority);
    }

    /// <summary>
    /// Registers a handler for the EXECUTE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during execution.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnExecutePhase(LSPhaseHandler<TEvent> handler, LSESPriority priority = LSESPriority.NORMAL) {
        return registerEventSpecificHandler(handler, LSEventPhase.EXECUTE, priority);
    }

    /// <summary>
    /// Registers a handler for the COMPLETE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during completion.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnCompletePhase(LSPhaseHandler<TEvent> handler, LSESPriority priority = LSESPriority.NORMAL) {
        return registerEventSpecificHandler(handler, LSEventPhase.COMPLETE, priority);
    }

    /// <summary>
    /// Registers a handler for the CANCEL phase.
    /// </summary>
    /// <param name="handler">The handler to execute during cancellation.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnCancelPhase(LSPhaseHandler<TEvent> handler, LSESPriority priority = LSESPriority.NORMAL) {
        return registerEventSpecificHandler(handler, LSEventPhase.CANCEL, priority);
    }

    /// <summary>
    /// Registers a handler for the SUCCESS phase.
    /// </summary>
    /// <param name="handler">The handler to execute during success.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnSuccessPhase(LSPhaseHandler<TEvent> handler, LSESPriority priority = LSESPriority.NORMAL) {
        return registerEventSpecificHandler(handler, LSEventPhase.SUCCESS, priority);
    }

       /// <summary>
       /// Registers a handler for the FAILURE phase.
       /// </summary>
       /// <param name="handler">The handler to execute during failure.</param>
       /// <param name="priority">The execution priority (default: NORMAL).</param>
       /// <returns>This builder for fluent chaining.</returns>
       public LSEventCallbackBuilder<TEvent> OnFailurePhase(LSPhaseHandler<TEvent> handler, LSESPriority priority = LSESPriority.NORMAL) {
           return registerEventSpecificHandler(handler, LSEventPhase.FAILURE, priority);
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
        return registerEventSpecificHandler(
            (evt, ctx) => {
                action(evt);
                return LSHandlerResult.CONTINUE;
            },
            LSEventPhase.COMPLETE,
            LSESPriority.NORMAL,
            evt => !string.IsNullOrEmpty(getErrorMessage(evt))
        );
    }

    /// <summary>
    /// Registers a simple action that executes during the CANCEL phase when the event is cancelled.
    /// For complex cancellation logic with conditions or phase-specific handling, use OnComplete
    /// with your own conditional logic.
    /// </summary>
    /// <param name="action">The action to execute on cancellation</param>
    /// <returns>This builder for fluent chaining</returns>
    public LSEventCallbackBuilder<TEvent> OnCancel(LSAction<TEvent>? action, LSESPriority priority = LSESPriority.NORMAL) {
        if (action == null) return this; //this prevent errors when using LSEventOptions
        return registerEventSpecificHandler(
            (evt, _) => {
                action(evt);
                return LSHandlerResult.CONTINUE;
            },
            LSEventPhase.CANCEL,
            priority
        );
    }

    /// <summary>
    /// Registers a simple action that executes when the event completes successfully.
    /// </summary>
    /// <param name="action">Action to execute on success</param>
    /// <returns>This builder for fluent chaining</returns>
    public LSEventCallbackBuilder<TEvent> OnSuccess(LSAction<TEvent>? action, LSESPriority priority = LSESPriority.NORMAL) {
        if (action == null) return this; //this is to prevent errors when using LSEventOptions
        return registerEventSpecificHandler(
            (evt, ctx) => {
                action(evt);
                return LSHandlerResult.CONTINUE;
            },
            LSEventPhase.SUCCESS,
            priority,
            null
        );
    }
    public LSEventCallbackBuilder<TEvent> OnComplete(LSAction<TEvent>? action, LSESPriority priority = LSESPriority.NORMAL) {
        if (action == null) return this; //this is to prevent errors when using LSEventOptions
        return registerEventSpecificHandler(
            (evt, ctx) => {
                action(evt);
                return LSHandlerResult.CONTINUE;
            },
            LSEventPhase.COMPLETE,
            priority,
            null
        );
    }

       /// <summary>
       /// Registers a simple action that executes during the FAILURE phase when the event has failed.
       /// </summary>
       /// <param name="action">Action to execute on failure</param>
       /// <param name="priority">The execution priority (default: NORMAL).</param>
       /// <returns>This builder for fluent chaining</returns>
       public LSEventCallbackBuilder<TEvent> OnFailure(LSAction<TEvent>? action, LSESPriority priority = LSESPriority.NORMAL) {
           if (action == null) return this;
           return registerEventSpecificHandler(
               (evt, ctx) => {
                   action(evt);
                   return LSHandlerResult.CONTINUE;
               },
               LSEventPhase.FAILURE,
               priority,
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
        return OnCompletePhase((evt, ctx) => {
            var elapsed = System.DateTime.UtcNow - startTime;
            callback(evt, elapsed);
            return LSHandlerResult.CONTINUE;
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
        return registerEventSpecificHandler((evt, ctx) => {
            if (condition(evt)) {
                if (!string.IsNullOrEmpty(errorMessage)) {
                    setErrorMessage(evt, errorMessage);
                }
                return LSHandlerResult.CANCEL;
            }
            return LSHandlerResult.CONTINUE;
        }, phase);
    }

    /// <summary>
    /// Retries a handler on error up to the maximum retry count.
    /// </summary>
    /// <param name="handler">The handler to retry.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> RetryOnError(LSPhaseHandler<TEvent> handler, int maxRetries = 3) {
        return registerEventSpecificHandler((evt, ctx) => {
            try {
                return handler(evt, ctx);
            } catch (LSException ex) {
                var retryKey = $"retry.attempt.{ctx.CurrentPhase}";
                var currentRetries = evt.TryGetData<int>(retryKey, out var count) ? count : 0;

                if (currentRetries < maxRetries) {
                    evt.SetData(retryKey, currentRetries + 1);
                    evt.SetData($"retry.error.{currentRetries}", ex.Message);
                    return LSHandlerResult.RETRY;
                }

                setErrorMessage(evt, $"Handler failed after {maxRetries} retries: {ex.Message}");
                return LSHandlerResult.CANCEL;
            }
        }, LSEventPhase.EXECUTE);
    }

    #endregion

    #region Processing

    /// <summary>
    /// Dispatches the event with registered event-scoped handlers and integrates with global handlers.
    /// 
    /// This method coordinates the execution of both event-scoped handlers (registered through this builder)
    /// and global handlers (registered directly on the dispatcher). The integration ensures that:
    /// - Event-scoped handlers execute only for this specific event instance
    /// - Global handlers execute for all events of this type
    /// - All handlers execute in proper phase and priority order
    /// - Automatic cleanup occurs after one-time execution
    /// 
    /// Even if no event-scoped handlers are registered, global handlers will still be processed,
    /// making this method suitable for all scenarios.
    /// </summary>
    /// <returns>
    /// True if the event completed successfully through all phases,
    /// false if it was cancelled, had critical failures, or is waiting for async operations.
    /// </returns>
    public bool Dispatch() {
        try {
            // Register the batch if not already registered and if we have handlers
            if (!_isRegistered && _handlers.Any()) {
                // Validate batch before registration
                var validationIssues = validateBatch();
                _validationWarnings.AddRange(validationIssues);
                
                // Finalize the batch for optimal execution
                finalizeBatch();
                
                // Register with dispatcher
                Dispatcher.registerBatchedHandlers(this);
                _isRegistered = true;
            }

            // Always process the event, even if no event-scoped handlers exist
            // This ensures global handlers registered on the dispatcher are executed
            return Dispatcher.processEvent(_event);
        } finally {
            // Auto-cleanup: the batch handler will automatically clean up after one execution
            // No manual cleanup needed due to MaxExecutions = 1 in RegisterBatchedHandlers
        }
    }
    
    /// <summary>
    /// Validates the current handler configuration and returns any issues found.
    /// This is automatically called during Dispatch() but can be called manually for early validation.
    /// </summary>
    /// <returns>List of validation warnings or errors.</returns>
    public List<string> Validate() {
        var issues = validateBatch();
        _validationWarnings.Clear();
        _validationWarnings.AddRange(issues);
        return new List<string>(issues);
    }

    #endregion

    #region Private Helper Methods

    private LSEventCallbackBuilder<TEvent> registerEventSpecificHandler(
        LSPhaseHandler<TEvent> handler,
        LSEventPhase phase,
        LSESPriority priority = LSESPriority.NORMAL,
        LSEventCondition<TEvent>? condition = null) {

        // Add handler to internal collection instead of using batch methods
        addHandler(handler, phase, priority, condition);

        return this;
    }

    /// <summary>
    /// Helper method to get error message from an event.
    /// </summary>
    /// <param name="evt">The event to get error message from.</param>
    /// <returns>The error message or null if none exists.</returns>
    private static string? getErrorMessage(TEvent evt) {
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
    private static void setErrorMessage(TEvent evt, string message) {
        if (evt is LSBaseEvent baseEvent) {
            baseEvent.ErrorMessage = message;
        } else {
            // Fallback to data dictionary for non-LSBaseEvent implementations
            evt.SetData("error.message", message);
        }
    }

    #endregion
}
