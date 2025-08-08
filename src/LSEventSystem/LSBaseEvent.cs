using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace LSUtils.EventSystem;

/// <summary>
/// Clean event interface - represents an event as a pure data container with state tracking.
/// Events are immutable from the handler perspective and only contain data and state information.
/// </summary>
public interface ILSEvent {
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    Guid Id { get; }
    
    /// <summary>
    /// The concrete type of this event for type-safe handling.
    /// </summary>
    Type EventType { get; }
    
    /// <summary>
    /// UTC timestamp when this event was created.
    /// </summary>
    DateTime CreatedAt { get; }
    
    /// <summary>
    /// Indicates if the event processing was cancelled.
    /// </summary>
    bool IsCancelled { get; }
    
    /// <summary>
    /// Indicates if the event has completed processing through all phases successfully.
    /// </summary>
    bool IsCompleted { get; }
    
    /// <summary>
    /// The current phase being executed.
    /// </summary>
    LSEventPhase CurrentPhase { get; }
    
    /// <summary>
    /// Flags indicating which phases have been completed successfully.
    /// </summary>
    LSEventPhase CompletedPhases { get; }
    
    /// <summary>
    /// Read-only access to event data stored as key-value pairs.
    /// </summary>
    IReadOnlyDictionary<string, object> Data { get; }
    
    /// <summary>
    /// Sets data associated with this event.
    /// </summary>
    /// <param name="key">The key to store the data under.</param>
    /// <param name="value">The value to store.</param>
    void SetData(string key, object value);
    
    /// <summary>
    /// Gets strongly-typed data associated with this event.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="key">The key to retrieve data for.</param>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found.</exception>
    /// <exception cref="InvalidCastException">Thrown when the data cannot be cast to the specified type.</exception>
    T GetData<T>(string key);
    
    /// <summary>
    /// Attempts to get strongly-typed data associated with this event.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="key">The key to retrieve data for.</param>
    /// <param name="value">The retrieved value if successful.</param>
    /// <returns>True if the data was found and successfully cast to the specified type.</returns>
    bool TryGetData<T>(string key, out T value);
}

/// <summary>
/// Internal interface for state mutations during event processing.
/// This is only used by the event dispatcher and should not be implemented by user code.
/// </summary>
internal interface ILSMutableEvent : ILSEvent {
    /// <summary>
    /// Sets whether the event is cancelled.
    /// </summary>
    new bool IsCancelled { get; set; }
    
    /// <summary>
    /// Sets whether the event is completed.
    /// </summary>
    new bool IsCompleted { get; set; }
    
    /// <summary>
    /// Sets the current phase being executed.
    /// </summary>
    new LSEventPhase CurrentPhase { get; set; }
    
    /// <summary>
    /// Sets which phases have been completed.
    /// </summary>
    new LSEventPhase CompletedPhases { get; set; }
}

/// <summary>
/// Phase handler delegate that processes events in a specific phase.
/// Handlers should be pure functions that don't modify event state directly.
/// </summary>
/// <typeparam name="TEvent">The event type this handler processes.</typeparam>
/// <param name="event">The event being processed.</param>
/// <param name="context">Execution context with phase information and metrics.</param>
/// <returns>Result indicating how event processing should continue.</returns>
public delegate LSPhaseResult LSPhaseHandler<in TEvent>(TEvent @event, LSPhaseContext context) where TEvent : ILSEvent;

/// <summary>
/// Clean event base class that provides a pure data container implementation.
/// This class handles the core event functionality while remaining lightweight and focused.
/// </summary>
public abstract class LSBaseEvent : ILSMutableEvent {
    private readonly ConcurrentDictionary<string, object> _data = new();
    
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();
    
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
    /// Optional error message if the event encountered an error.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Read-only access to event data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data => _data;
    
    /// <summary>
    /// Initializes a new event with its concrete type.
    /// </summary>
    protected LSBaseEvent() {
        EventType = GetType();
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
}

/// <summary>
/// Generic event with strongly-typed instance that provides type-safe access to the 
/// source object that triggered the event. This is the recommended base class for 
/// most domain events.
/// </summary>
/// <typeparam name="TInstance">The type of the object that triggered this event.</typeparam>
public abstract class LSEvent<TInstance> : LSBaseEvent where TInstance : class {
    /// <summary>
    /// The strongly-typed instance that triggered this event.
    /// This provides direct access to the source object for event handlers.
    /// </summary>
    public TInstance Instance { get; }
    
    /// <summary>
    /// Initializes a new event with the specified instance.
    /// </summary>
    /// <param name="instance">The instance that triggered this event.</param>
    /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
    protected LSEvent(TInstance instance) {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
    }
}
