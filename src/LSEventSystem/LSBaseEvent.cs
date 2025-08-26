using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace LSUtils.EventSystem;

/// <summary>
/// Abstract base class that provides a complete implementation of the event system's core functionality.
/// This class serves as the foundation for all events in the system, providing data storage,
/// state tracking, and integration with the event processing pipeline.
/// 
/// This implementation focuses on:
/// - Thread-safe data storage using ConcurrentDictionary
/// - Automatic event metadata generation (ID, type, timestamps)
/// - Clean separation between public and internal APIs
/// - Integration with the LSDispatcher for event processing
/// - Event-scoped callback builder for inline handler registration
/// - Fluent API for event processing with automatic cleanup
/// </summary>
/// <remarks>
/// Inherit from this class when creating events that don't need a specific source instance.
/// For events tied to a specific object, consider using LSEvent&lt;TInstance&gt; instead.
/// 
/// The event system now supports three main approaches for handler registration:
/// 1. Global handlers via dispatcher.For&lt;TEvent&gt;().Register(handler)
/// 2. Event-scoped handlers via event.RegisterCallback&lt;TEvent&gt;(dispatcher)
/// 3. Inline processing via event.ProcessWith&lt;TEvent&gt;(dispatcher, builder => ...)
/// 
/// Example usage:
/// <code>
/// // Traditional approach with global handlers
/// public class SystemStartupEvent : LSBaseEvent {
///     public string Version { get; }
///     public DateTime StartupTime { get; }
///     
///     public SystemStartupEvent(string version) {
///         Version = version;
///         StartupTime = DateTime.UtcNow;
///     }
/// }
/// 
/// // Usage with inline callbacks (recommended for event-specific logic)
/// var startupEvent = new SystemStartupEvent("1.0.0");
/// var success = startupEvent.ProcessWith&lt;SystemStartupEvent&gt;(dispatcher, builder => builder
///     .OnValidation((evt, ctx) => ValidateSystem(evt.Version))
///     .OnExecution((evt, ctx) => StartServices(evt))
///     .OnSuccess((evt, ctx) => LogStartupSuccess(evt))
///     .OnError((evt, ctx) => LogStartupError(evt))
/// );
/// </code>
/// </remarks>
public abstract class LSBaseEvent : ILSMutableEvent {
    private readonly ConcurrentDictionary<string, object> _data = new();

    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid ID { get; } = Guid.NewGuid();

    /// <summary>
    /// The concrete type of this event.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// UTC timestamp when this event was created.
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates if the event processing was cancelled.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Indicates if the event has completed processing successfully.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// The current phase being executed.
    /// </summary>
    public LSEventPhase CurrentPhase { get; set; } = LSEventPhase.VALIDATE;

    /// <summary>
    /// Flags indicating which phases have been completed.
    /// </summary>
    public LSEventPhase CompletedPhases { get; set; }

    /// <summary>
    /// Indicates if the event is currently waiting for an async operation to complete.
    /// </summary>
    public bool IsWaiting { get; set; }

    /// <summary>
    /// Reference to the dispatcher processing this event (used for resumption).
    /// </summary>
    private LSDispatcher? _dispatcher;

    /// <summary>
    /// Optional error message if the event encountered an error.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Read-only access to event data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data => _data;

    /// <summary>
    /// Initializes a new event with its concrete type.
    /// The EventType property is set to the actual runtime type of the event instance.
    /// </summary>
    protected LSBaseEvent() {
        EventType = this.GetType();
    }

    /// <summary>
    /// Sets data associated with this event.
    /// </summary>
    /// <param name="key">The key to store the data under.</param>
    /// <param name="value">The value to store.</param>
    public void SetData(string key, object value) => _data[key] = value;

    /// <summary>
    /// Sets an error message for this event.
    /// </summary>
    /// <param name="message">The error message to set.</param>
    public void SetErrorMessage(string message) {
        ErrorMessage = message;
    }

    /// <summary>
    /// Gets strongly-typed data associated with this event.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="key">The key to retrieve data for.</param>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found.</exception>
    /// <exception cref="InvalidCastException">Thrown when the data cannot be cast to the specified type.</exception>
    public T GetData<T>(string key) => (T)_data[key];

    /// <summary>
    /// Attempts to get strongly-typed data associated with this event.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="key">The key to retrieve data for.</param>
    /// <param name="value">The retrieved value if successful.</param>
    /// <returns>True if the data was found and successfully cast to the specified type.</returns>
    public bool TryGetData<T>(string key, out T value) {
        if (_data.TryGetValue(key, out var obj) && obj is T typed) {
            value = typed;
            return true;
        }
        value = default(T)!;
        return false;
    }

    /// <summary>
    /// Legacy method kept for compatibility.
    /// </summary>
    /// <returns>Always returns false.</returns>
    public bool Signal() {
        // This method is kept for compatibility but doesn't do anything currently
        // In the future, this could be used for additional signaling mechanisms
        return false;
    }

    /// <summary>
    /// Signals that an async operation has completed and event processing should resume.
    /// This method is thread-safe and will notify the dispatcher to continue processing.
    /// </summary>
    /// <remarks>
    /// This method should be called by the event or handler that initiated the WAITING state.
    /// The dispatcher will resume processing immediately.
    /// </remarks>
    public void ContinueProcessing() {
        if (_dispatcher == null) {
            throw new InvalidOperationException("Cannot continue processing without a dispatcher reference. " +
                "Ensure the event was processed using Process() or ProcessWith() methods.");
        }
        
        if (!IsWaiting) {
            throw new InvalidOperationException("Event is not in a waiting state. ContinueProcessing() should only be called when IsWaiting is true.");
        }
        
        IsWaiting = false;
        
        // Resume processing through the dispatcher using the unified method
        _dispatcher.ContinueProcessing(this);
    }

    /// <summary>
    /// Creates a callback builder for registering event-specific handlers that execute only for this event instance.
    /// This provides a fluent API for setting up one-time handlers with automatic cleanup.
    /// </summary>
    /// <typeparam name="TEventType">The concrete event type for type-safe handler registration.</typeparam>
    /// <param name="dispatcher">The dispatcher to register handlers with.</param>
    /// <returns>A callback builder that allows fluent configuration of event-specific handlers.</returns>
    /// <example>
    /// <code>
    /// var success = myEvent.RegisterCallback&lt;MyEvent&gt;(dispatcher)
    ///     .OnValidation((evt, ctx) => ValidateEvent(evt))
    ///     .OnExecution((evt, ctx) => ProcessEvent(evt))
    ///     .OnError((evt, ctx) => LogError(evt.ErrorMessage))
    ///     .ProcessAndCleanup();
    /// </code>
    /// </example>
    public LSEventCallbackBuilder<TEventType> RegisterCallback<TEventType>(LSDispatcher dispatcher)
        where TEventType : ILSEvent {
        return new LSEventCallbackBuilder<TEventType>((TEventType)(object)this, dispatcher);
    }
    
    /// <summary>
    /// Convenience method that creates a callback builder, applies configuration, and processes the event.
    /// This is the recommended approach for simple event processing with inline handlers.
    /// </summary>
    /// <typeparam name="TEventType">The concrete event type for type-safe handler registration.</typeparam>
    /// <param name="dispatcher">The dispatcher to use for processing.</param>
    /// <param name="configure">Optional configuration action for the callback builder.</param>
    /// <returns>True if the event completed successfully, false if it was cancelled or had errors.</returns>
    /// <example>
    /// <code>
    /// var success = myEvent.ProcessWith&lt;MyEvent&gt;(dispatcher, builder => builder
    ///     .OnValidation((evt, ctx) => ValidateEvent(evt))
    ///     .OnSuccess((evt, ctx) => LogSuccess(evt))
    ///     .OnError((evt, ctx) => LogError(evt.ErrorMessage))
    /// );
    /// </code>
    /// </example>
    public bool ProcessWith<TEventType>(LSDispatcher dispatcher, 
        Action<LSEventCallbackBuilder<TEventType>>? configure = null) 
        where TEventType : ILSEvent {
        //dont allow process if the event already has been dispatched

        // Set dispatcher reference for async resumption
        _dispatcher = dispatcher;
        
        var builder = RegisterCallback<TEventType>(dispatcher);
        configure?.Invoke(builder);
        return builder.ProcessAndCleanup();
    }

    /// <summary>
    /// Processes this event through the provided dispatcher's event processing pipeline.
    /// This is a convenience method that submits the event to the dispatcher for phase-based processing.
    /// </summary>
    /// <param name="dispatcher">The event dispatcher that will process this event through its registered handlers.</param>
    /// <returns>
    /// true if the event was processed successfully and completed all phases;
    /// false if the event was cancelled during processing or encountered an error.
    /// </returns>
    /// <remarks>
    /// This method provides a fluent interface for event processing. The event will be processed
    /// through all registered phases according to the dispatcher's configuration.
    /// 
    /// Example usage:
    /// <code>
    /// var myEvent = new UserRegistrationEvent(user);
    /// bool success = myEvent.Process(dispatcher);
    /// 
    /// if (success) {
    ///     // Event completed successfully
    /// } else {
    ///     // Event was cancelled or failed
    ///     Console.WriteLine($"Error: {myEvent.ErrorMessage}");
    /// }
    /// </code>
    /// </remarks>
    public bool Process(LSDispatcher dispatcher) {
        // Set dispatcher reference for async resumption
        _dispatcher = dispatcher;
        
        return dispatcher.ProcessEvent(this);
    }

}
