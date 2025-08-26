using System;
using System.Collections.Generic;

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
/// **Conditional Methods:**
/// - OnError (state-based handlers)
/// - OnCancel(action, priority) - any-phase cancellation handlers
/// - OnCancel(phase, action, priority) - phase-specific cancellation handlers  
/// - OnCancelWhen(action, condition, priority) - conditional cancellation handlers
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
/// var success = myEvent.ProcessWith&lt;MyEvent&gt;(dispatcher, builder => builder
///     .OnValidation((evt, ctx) => ValidateEvent(evt))
///     .OnExecution((evt, ctx) => ProcessEvent(evt))
///     .OnCancel(evt => CleanupOnCancel(evt))
/// );
/// 
/// // With priorities
/// var success = myEvent.ProcessWith&lt;MyEvent&gt;(dispatcher, builder => builder
///     .OnValidation(CriticalValidation, LSPhasePriority.CRITICAL)
///     .OnExecution(MainProcessing, LSPhasePriority.NORMAL)
/// );
/// </code>
/// </remarks>
///     .OnExecution((evt, ctx) => ProcessEvent(evt))
///     .OnSuccess((evt, ctx) => LogSuccess(evt))
/// );
/// 
/// // Complex event pipeline
/// myEvent.ProcessWith&lt;MyEvent&gt;(dispatcher, builder => builder
///     .SetData("start.time", DateTime.UtcNow)
///     .ValidateData&lt;string&gt;("user.email", email => !string.IsNullOrEmpty(email), "Email required")
///     .OnTimeout(TimeSpan.FromSeconds(30), (evt, ctx) => HandleTimeout(evt))
///     .TransformData&lt;string&gt;("user.name", name => name.Trim().ToLower())
///     .OnSuccess((evt, ctx) => SendConfirmationEmail(evt))
///     .OnError((evt, ctx) => LogError(evt))
///     .MeasureExecutionTime((evt, time) => RecordMetrics(evt, time))
/// );
/// 
/// // Manual lifecycle management
/// var builder = myEvent.RegisterCallback&lt;MyEvent&gt;(dispatcher);
/// builder.OnValidation(ValidateHandler)
///        .OnExecution(ExecuteHandler);
/// var success = builder.ProcessAndCleanup();
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
    /// Registers a handler for the SUCCESS phase.
    /// This phase only runs when the event completes successfully (not cancelled).
    /// </summary>
    /// <param name="handler">The handler to execute on success.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnSuccess(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.SUCCESS, priority);
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
    /// Registers a handler that only executes if the event has an error.
    /// </summary>
    /// <param name="handler">The handler to execute on error.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnError(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(
            handler, 
            LSEventPhase.COMPLETE, 
            LSPhasePriority.NORMAL,
            evt => !string.IsNullOrEmpty(GetErrorMessage(evt))
        );
    }
    
    /// <summary>
    /// Registers a simple action that executes during the CANCEL phase when the event is cancelled.
    /// This handler will run regardless of which phase the cancellation occurred in.
    /// </summary>
    /// <param name="action">The action to execute on cancellation.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnCancel(LSAction<TEvent> action, LSPhasePriority priority = LSPhasePriority.NORMAL) {
        return RegisterEventSpecificHandler(
            (evt, _) => {
                action(evt);
                return LSPhaseResult.CONTINUE;
            }, 
            LSEventPhase.CANCEL, 
            priority
        );
    }
    
    /// <summary>
    /// Registers a phase-specific cancellation handler that only executes if the event was cancelled 
    /// during the specified phase(s). Supports multiple phases using flag combinations.
    /// </summary>
    /// <param name="phase">The phase(s) during which cancellation must have occurred for this handler to run.</param>
    /// <param name="action">The action to execute on cancellation.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnCancel(LSEventPhase phase, LSAction<TEvent> action, LSPhasePriority priority = LSPhasePriority.NORMAL) {
        return RegisterEventSpecificHandler(
            (evt, _) => {
                // Check if cancellation occurred during the specified phase(s)
                if (evt.Data.TryGetValue("cancel.phase", out var cancelPhaseData) && 
                    cancelPhaseData is LSEventPhase cancelPhase) {
                    
                    // Support multiple phases using flag operations
                    if ((phase & cancelPhase) != 0) {
                        action(evt);
                    }
                }
                return LSPhaseResult.CONTINUE;
            }, 
            LSEventPhase.CANCEL, 
            priority
        );
    }
    
    /// <summary>
    /// Registers a conditional cancellation handler that executes during the CANCEL phase
    /// only if the specified condition is met at the time of cancellation.
    /// </summary>
    /// <param name="action">The action to execute on cancellation.</param>
    /// <param name="condition">The condition that must be true for the handler to execute.</param>
    /// <param name="priority">The execution priority (default: NORMAL).</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnCancelWhen(LSAction<TEvent> action, Func<TEvent, bool> condition, LSPhasePriority priority = LSPhasePriority.NORMAL) {
        return RegisterEventSpecificHandler(
            (evt, _) => {
                if (condition(evt)) {
                    action(evt);
                }
                return LSPhaseResult.CONTINUE;
            }, 
            LSEventPhase.CANCEL, 
            priority
        );
    }
    
    #endregion
    
    #region Logging and Monitoring Methods
    
    /// <summary>
    /// Logs phase execution information.
    /// </summary>
    /// <param name="logger">The logging action.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> LogPhase(Action<string> logger) {
        // Register for all phases
        foreach (LSEventPhase phase in Enum.GetValues<LSEventPhase>()) {
            RegisterEventSpecificHandler((evt, ctx) => {
                logger($"Event {evt.ID} executing in phase {ctx.CurrentPhase}");
                return LSPhaseResult.CONTINUE;
            }, phase, LSPhasePriority.BACKGROUND);
        }
        return this;
    }
    
    /// <summary>
    /// Logs error information when the event fails.
    /// </summary>
    /// <param name="logger">The logging action.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> LogOnError(Action<TEvent, string> logger) {
        return OnError((evt, ctx) => {
            var errorMsg = GetErrorMessage(evt);
            if (!string.IsNullOrEmpty(errorMsg)) {
                logger(evt, errorMsg);
            }
            return LSPhaseResult.CONTINUE;
        });
    }
    
    /// <summary>
    /// Measures and reports execution time.
    /// </summary>
    /// <param name="callback">The callback to receive timing information.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> MeasureExecutionTime(Action<TEvent, TimeSpan> callback) {
        var startTime = DateTime.UtcNow;
        return OnComplete((evt, ctx) => {
            var elapsed = DateTime.UtcNow - startTime;
            callback(evt, elapsed);
            return LSPhaseResult.CONTINUE;
        });
    }
    
    #endregion
    
    #region Error Handling Methods
    
    /// <summary>
    /// Cancels the event if a condition is met.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="errorMessage">The error message to set.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> CancelIf(Func<TEvent, bool> condition, string errorMessage = "") {
        return RegisterEventSpecificHandler((evt, ctx) => {
            if (condition(evt)) {
                if (!string.IsNullOrEmpty(errorMessage)) {
                    SetErrorMessage(evt, errorMessage);
                }
                return LSPhaseResult.CANCEL;
            }
            return LSPhaseResult.CONTINUE;
        }, LSEventPhase.VALIDATE);
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
            } catch (Exception ex) {
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
    
    #region Chaining and Composition Methods
    
    /// <summary>
    /// Executes a configuration action on this builder.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> Then(Action<LSEventCallbackBuilder<TEvent>> configure) {
        configure(this);
        return this;
    }
    
    /// <summary>
    /// Conditionally executes configuration based on the event state.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="configure">The configuration action if condition is true.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> If(Func<TEvent, bool> condition, Action<LSEventCallbackBuilder<TEvent>> configure) {
        if (condition(_event)) {
            configure(this);
        }
        return this;
    }
    
    /// <summary>
    /// Registers multiple handlers to execute in parallel within the same phase.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> InParallel(params LSPhaseHandler<TEvent>[] handlers) {
        foreach (var handler in handlers) {
            RegisterEventSpecificHandler(handler, LSEventPhase.EXECUTE);
        }
        return this;
    }
    
    #endregion
    
    #region Processing and Cleanup
    
    /// <summary>
    /// Processes the event and automatically cleans up registered handlers.
    /// </summary>
    /// <returns>True if the event completed successfully, false if it was cancelled or had errors.</returns>
    public bool ProcessAndCleanup() {
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
        Func<TEvent, bool>? condition = null) {
        
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
