using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Core event interface representing an immutable data container with comprehensive state tracking.
/// Events serve as data carriers throughout the event processing pipeline, providing access to 
/// event metadata, processing state information, and associated data while maintaining clean 
/// separation between event data and processing logic.
/// 
/// Events are designed to be self-contained units of information that can be processed through 
/// multiple phases without requiring external state management. All state properties are 
/// read-only from the handler perspective to ensure immutability.
/// </summary>
public interface ILSEvent {
    /// <summary>
    /// Unique identifier for this event instance.
    /// Generated automatically when the event is created and remains constant throughout
    /// the event's entire lifecycle.
    /// </summary>
    System.Guid ID { get; }

    /// <summary>
    /// UTC timestamp when this event was created.
    /// Provides timing information for event processing analytics, debugging, and audit trails.
    /// </summary>
    System.DateTime CreatedAt { get; }

    /// <summary>
    /// Indicates whether the event processing was cancelled by a handler.
    /// When true, no further phase processing will occur.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Indicates whether the event has failures.
    /// When true, the event will proceed depending on the phase behaviour.
    /// </summary>
    bool HasFailures { get; }

    /// <summary>
    /// Indicates whether the event has completed processing through all phases.
    /// An event is considered completed when the event processing lifecycle is finished.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Associates data with this event using a string key.
    /// Allows handlers to store information that persists for the lifetime of the event
    /// and can be accessed by subsequent handlers in the processing pipeline.
    /// </summary>
    /// <param name="key">The unique key to store the data under.</param>
    /// <param name="value">The data value to store.</param>
    void SetData<T>(string key, T value);

    /// <summary>
    /// Retrieves strongly-typed data associated with this event.
    /// Provides type-safe access to stored event data with compile-time type checking.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified key is not found.</exception>
    /// <exception cref="InvalidCastException">Thrown when the stored data cannot be cast to the specified type.</exception>
    T GetData<T>(string key);

    /// <summary>
    /// Attempts to retrieve strongly-typed data associated with this event.
    /// Provides safe access to stored event data without throwing exceptions for missing keys or type mismatches.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <param name="value">When this method returns, contains the retrieved value if successful, or the default value for T if unsuccessful.</param>
    /// <returns>true if the data was found and successfully cast to the specified type; otherwise, false.</returns>
    bool TryGetData<T>(string key, out T value);

    /// <summary>
    /// Enable a context for this event.
    /// this will be merged with the root context before processing.
    /// <see cref="LSEventContextManager"/> root context is a parallel root node with no number.
    /// Return the event instance to allow chaining.
    /// </summary>
    /// <param name="subBuilder"></param>
    /// <returns></returns>
    ILSEvent Context(LSEventSubContextBuilder subBuilder);

    /// <summary>
    /// Process the event through a context manager (or Singleton if not provided).
    /// </summary>
    LSEventProcessStatus Process(ILSEventable? instance = null, LSEventContextManager? contextManager = null);
    LSEventProcessStatus Resume(params string[] nodeIDs); // process context should be stored in the event instance
    LSEventProcessStatus Fail(params string[] nodeIDs); // process context should be stored in the event instance
    void Cancel();
}
