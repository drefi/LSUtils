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
    /// When true, no further phase processing will occur except CANCEL and COMPLETE phases.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Indicates whether the event has failures but processing can continue.
    /// When true, the event will proceed to the FAILURE phase instead of SUCCESS phase.
    /// </summary>
    bool HasFailures { get; }

    /// <summary>
    /// Indicates whether the event has completed processing through all phases.
    /// An event is considered completed when all applicable phases have been executed.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// The current phase being executed in the event processing pipeline.
    /// Provides real-time visibility into the event's progress through the system.
    /// </summary>
    LSEventPhase CurrentPhase { get; }

    /// <summary>
    /// Flags indicating which phases have been completed successfully.
    /// Uses bitwise operations to efficiently track multiple completed phases.
    /// </summary>
    LSEventPhase CompletedPhases { get; }

    /// <summary>
    /// Read-only access to event data stored as key-value pairs.
    /// Contains all custom data associated with the event, allowing handlers 
    /// to share information across phases.
    /// </summary>
    System.Collections.Generic.IReadOnlyDictionary<string, object> Data { get; }

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
}
