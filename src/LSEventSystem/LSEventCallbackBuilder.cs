using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Event-scoped callback builder that provides a fluent API for registering one-time handlers
/// specific to a particular event instance. This builder automatically manages handler lifecycle
/// and cleanup, making it ideal for event-specific processing logic.
/// 
/// The callback builder provides several categories of methods:
/// 
/// **Core Phase Methods:**
/// - OnValidation, OnPrepare, OnExecution, OnFinalize, OnComplete
/// - OnCriticalValidation, OnHighPriorityExecution (priority variants)
/// 
/// **Conditional Methods:**
/// - OnSuccess, OnError, OnCancel (state-based handlers)
/// - OnDataPresent, OnDataEquals (data-based conditions)
/// 
/// **Timing Methods:**
/// - OnTimeout, OnSlowProcessing, OnPhaseChange
/// - MeasureExecutionTime (performance monitoring)
/// 
/// **Action Shortcuts:**
/// - DoOnValidation, DoOnExecution, DoOnComplete (simple actions)
/// - DoWhen (conditional actions)
/// 
/// **Data Manipulation:**
/// - SetData, SetDataWhen, TransformData
/// - ValidateData (with automatic error handling)
/// 
/// **Logging and Monitoring:**
/// - LogPhase, LogOnError (debugging and monitoring)
/// 
/// **Error Handling:**
/// - CancelIf, RetryOnError (error management)
/// 
/// **Composition:**
/// - Then, If, InParallel (advanced flow control)
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
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnValidation(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.VALIDATE);
    }
    
    /// <summary>
    /// Registers a handler for the PREPARE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during preparation.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnPrepare(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.PREPARE);
    }
    
    /// <summary>
    /// Registers a handler for the EXECUTE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during execution.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnExecution(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.EXECUTE);
    }
    
    /// <summary>
    /// Registers a handler for the FINALIZE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during finalization.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnFinalize(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.FINALIZE);
    }
    
    /// <summary>
    /// Registers a handler for the COMPLETE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during completion.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnComplete(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.COMPLETE);
    }
    
    /// <summary>
    /// Registers a high-priority handler for the VALIDATE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during validation.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnCriticalValidation(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.VALIDATE, LSPhasePriority.CRITICAL);
    }
    
    /// <summary>
    /// Registers a high-priority handler for the EXECUTE phase.
    /// </summary>
    /// <param name="handler">The handler to execute during execution.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnHighPriorityExecution(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(handler, LSEventPhase.EXECUTE, LSPhasePriority.HIGH);
    }
    
    #endregion
    
    #region Conditional and State-Based Methods
    
    /// <summary>
    /// Registers a handler that only executes if the event completes successfully.
    /// </summary>
    /// <param name="handler">The handler to execute on success.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnSuccess(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(
            handler, 
            LSEventPhase.COMPLETE, 
            LSPhasePriority.NORMAL,
            evt => !evt.IsCancelled && string.IsNullOrEmpty(GetErrorMessage(evt))
        );
    }
    
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
    /// Registers a handler that only executes if the event was cancelled.
    /// </summary>
    /// <param name="handler">The handler to execute on cancellation.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnCancel(LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(
            handler, 
            LSEventPhase.COMPLETE, 
            LSPhasePriority.NORMAL,
            evt => evt.IsCancelled
        );
    }
    
    /// <summary>
    /// Registers a handler that only executes if specific data is present.
    /// </summary>
    /// <param name="key">The data key to check for.</param>
    /// <param name="handler">The handler to execute if data is present.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnDataPresent(string key, LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(
            handler, 
            LSEventPhase.EXECUTE, 
            LSPhasePriority.NORMAL,
            evt => evt.Data.ContainsKey(key)
        );
    }
    
    /// <summary>
    /// Registers a handler that only executes if specific data equals a value.
    /// </summary>
    /// <typeparam name="T">The type of the data value.</typeparam>
    /// <param name="key">The data key to check.</param>
    /// <param name="value">The value to compare against.</param>
    /// <param name="handler">The handler to execute if data equals the value.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnDataEquals<T>(string key, T value, LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(
            handler, 
            LSEventPhase.EXECUTE, 
            LSPhasePriority.NORMAL,
            evt => evt.TryGetData<T>(key, out var data) && Equals(data, value)
        );
    }
    
    #endregion
    
    #region Timing and Performance Methods
    
    /// <summary>
    /// Registers a handler that executes if processing takes longer than specified timeout.
    /// </summary>
    /// <param name="timeout">The timeout threshold.</param>
    /// <param name="handler">The handler to execute on timeout.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnTimeout(TimeSpan timeout, LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler(
            handler, 
            LSEventPhase.COMPLETE, 
            LSPhasePriority.NORMAL,
            evt => DateTime.UtcNow - evt.CreatedAt > timeout
        );
    }
    
    /// <summary>
    /// Registers a handler that executes if processing is slower than expected.
    /// </summary>
    /// <param name="threshold">The performance threshold.</param>
    /// <param name="handler">The handler to execute for slow processing.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnSlowProcessing(TimeSpan threshold, LSPhaseHandler<TEvent> handler) {
        return RegisterEventSpecificHandler((evt, ctx) => {
            if (ctx.ElapsedTime > threshold) {
                return handler(evt, ctx);
            }
            return LSPhaseResult.CONTINUE;
        }, LSEventPhase.FINALIZE);
    }
    
    /// <summary>
    /// Registers a callback that executes whenever the event changes phases.
    /// </summary>
    /// <param name="callback">The callback to execute on phase changes.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> OnPhaseChange(Action<TEvent, LSEventPhase, LSEventPhase> callback) {
        var lastPhase = LSEventPhase.VALIDATE;
        
        // Register handlers for all phases to track changes
        foreach (LSEventPhase phase in Enum.GetValues<LSEventPhase>()) {
            RegisterEventSpecificHandler((evt, ctx) => {
                if (ctx.CurrentPhase != lastPhase) {
                    callback(evt, lastPhase, ctx.CurrentPhase);
                    lastPhase = ctx.CurrentPhase;
                }
                return LSPhaseResult.CONTINUE;
            }, phase, LSPhasePriority.BACKGROUND);
        }
        
        return this;
    }
    
    #endregion
    
    #region Action-Based Shortcuts
    
    /// <summary>
    /// Executes an action during validation without affecting flow control.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> DoOnValidation(Action<TEvent> action) {
        return OnValidation((evt, ctx) => { action(evt); return LSPhaseResult.CONTINUE; });
    }
    
    /// <summary>
    /// Executes an action during execution without affecting flow control.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> DoOnExecution(Action<TEvent> action) {
        return OnExecution((evt, ctx) => { action(evt); return LSPhaseResult.CONTINUE; });
    }
    
    /// <summary>
    /// Executes an action during completion without affecting flow control.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> DoOnComplete(Action<TEvent> action) {
        return OnComplete((evt, ctx) => { action(evt); return LSPhaseResult.CONTINUE; });
    }
    
    /// <summary>
    /// Executes an action conditionally without affecting flow control.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="action">The action to execute if condition is true.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> DoWhen(Func<TEvent, bool> condition, Action<TEvent> action) {
        return RegisterEventSpecificHandler(
            (evt, ctx) => { action(evt); return LSPhaseResult.CONTINUE; },
            LSEventPhase.EXECUTE,
            LSPhasePriority.NORMAL,
            condition
        );
    }
    
    #endregion
    
    #region Data Manipulation Methods
    
    /// <summary>
    /// Sets data on the event during validation.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <param name="value">The data value.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> SetData(string key, object value) {
        return DoOnValidation(evt => evt.SetData(key, value));
    }
    
    /// <summary>
    /// Sets data on the event conditionally.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="key">The data key.</param>
    /// <param name="value">The data value.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> SetDataWhen(Func<TEvent, bool> condition, string key, object value) {
        return DoWhen(condition, evt => evt.SetData(key, value));
    }
    
    /// <summary>
    /// Transforms existing data using a transformation function.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="key">The data key.</param>
    /// <param name="transform">The transformation function.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> TransformData<T>(string key, Func<T, T> transform) {
        return RegisterEventSpecificHandler((evt, ctx) => {
            if (evt.TryGetData<T>(key, out var value)) {
                var transformedValue = transform(value);
                if (transformedValue != null) {
                    evt.SetData(key, transformedValue);
                }
            }
            return LSPhaseResult.CONTINUE;
        }, LSEventPhase.EXECUTE);
    }
    
    /// <summary>
    /// Validates data and cancels the event if validation fails.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="key">The data key.</param>
    /// <param name="validator">The validation function.</param>
    /// <param name="errorMessage">The error message if validation fails.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public LSEventCallbackBuilder<TEvent> ValidateData<T>(string key, Func<T, bool> validator, string errorMessage) {
        return OnValidation((evt, ctx) => {
            if (evt.TryGetData<T>(key, out var value) && !validator(value)) {
                SetErrorMessage(evt, errorMessage);
                return LSPhaseResult.CANCEL;
            }
            return LSPhaseResult.CONTINUE;
        });
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
                logger($"Event {evt.Id} executing in phase {ctx.CurrentPhase}");
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
            case LSEventPhase.FINALIZE:
                _batch.OnFinalize(handler, priority, condition);
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
        evt.SetData("error.message", message);
    }
    
    #endregion
}
