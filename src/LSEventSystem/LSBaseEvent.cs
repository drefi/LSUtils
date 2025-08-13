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
/// </summary>
/// <remarks>
/// Inherit from this class when creating events that don't need a specific source instance.
/// For events tied to a specific object, consider using LSEvent&lt;TInstance&gt; instead.
/// 
/// Example usage:
/// <code>
/// public class SystemStartupEvent : LSBaseEvent {
///     public string Version { get; }
///     public DateTime StartupTime { get; }
///     
///     public SystemStartupEvent(string version) {
///         Version = version;
///         StartupTime = DateTime.UtcNow;
///     }
/// }
/// </code>
/// </remarks>
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
        return dispatcher.ProcessEvent(this);
    }

}
