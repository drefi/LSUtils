using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Core event interface that represents an event as a pure data container with comprehensive state tracking.
/// Events implement immutable semantics from the handler perspective and serve as data carriers
/// throughout the event processing pipeline.
/// 
/// This interface provides access to event metadata, processing state information, and associated data
/// while maintaining a clean separation between event data and processing logic. Events are designed
/// to be self-contained units of information that can be processed through multiple phases without
/// requiring external state management.
/// 
/// Key Design Principles:
/// - Events are immutable from the handler perspective
/// - State tracking is provided for observability and debugging
/// - Data storage is flexible and type-safe
/// - Event identity is maintained throughout processing
/// </summary>
public interface ILSEvent {
    /// <summary>
    /// Unique identifier for this event instance.
    /// Generated automatically when the event is created and remains constant throughout
    /// the event's entire lifecycle. This ID can be used for tracking, logging, and debugging
    /// event processing across multiple systems and phases.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    /// The concrete type of this event, used for type-safe event handling and registration.
    /// This allows the event system to route events to appropriate handlers based on their type
    /// and enables compile-time type checking for event handling code.
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// UTC timestamp when this event was created.
    /// Provides timing information for event processing analytics, debugging, and audit trails.
    /// Can be used to calculate processing time, detect delays, and implement timeout mechanisms.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Indicates whether the event processing was cancelled by a handler.
    /// When true, no further phase processing will occur for this event.
    /// Cancellation is typically used for validation failures, business rule violations,
    /// or when an error condition prevents further processing.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Indicates whether the event has completed processing through all registered phases successfully.
    /// An event is considered completed when all phases have been executed without cancellation or
    /// critical errors. This flag is useful for determining if an event fully completed its
    /// intended processing pipeline.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// The current phase being executed in the event processing pipeline.
    /// This provides real-time visibility into the event's progress through the system
    /// and can be used for monitoring, debugging, and progress tracking.
    /// </summary>
    LSEventPhase CurrentPhase { get; }

    /// <summary>
    /// Flags indicating which phases have been completed successfully.
    /// Uses bitwise operations to efficiently track multiple completed phases.
    /// This information is useful for debugging, audit trails, and understanding
    /// exactly how far an event progressed through the processing pipeline.
    /// </summary>
    LSEventPhase CompletedPhases { get; }

    /// <summary>
    /// Read-only access to event data stored as key-value pairs.
    /// This dictionary contains all custom data associated with the event,
    /// allowing handlers to share information across phases and providing
    /// a flexible mechanism for storing event-specific information.
    /// </summary>
    IReadOnlyDictionary<string, object> Data { get; }

    /// <summary>
    /// Associates data with this event using a string key.
    /// This method allows handlers to store information that can be accessed
    /// by subsequent handlers in the processing pipeline. Data stored here
    /// persists for the lifetime of the event and can be used for inter-handler
    /// communication, result accumulation, and state sharing.
    /// </summary>
    /// <param name="key">The unique key to store the data under. Should be descriptive and avoid conflicts.
    /// Consider using namespaced keys like "validation.errors" or "processing.result" to prevent collisions.</param>
    /// <param name="value">The data value to store. Can be any object type.
    /// For complex objects, ensure they are serializable if persistence is required.</param>
    /// <remarks>
    /// Best Practices:
    /// - Use meaningful, descriptive key names
    /// - Consider using namespaced keys to avoid conflicts
    /// - Store immutable objects when possible to prevent unintended modifications
    /// - Document expected data types and formats for shared keys
    /// </remarks>
    void SetData(string key, object value);

    /// <summary>
    /// Retrieves strongly-typed data associated with this event.
    /// This method provides type-safe access to stored event data with compile-time
    /// type checking and automatic casting to the expected type.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified key is not found in the event data.</exception>
    /// <exception cref="InvalidCastException">Thrown when the stored data cannot be cast to the specified type.</exception>
    /// <example>
    /// <code>
    /// // Retrieving typed data from an event
    /// var userId = myEvent.GetData&lt;int&gt;("user.id");
    /// var userName = myEvent.GetData&lt;string&gt;("user.name");
    /// var processingResult = myEvent.GetData&lt;ProcessingResult&gt;("processing.result");
    /// </code>
    /// </example>
    T GetData<T>(string key);

    /// <summary>
    /// Attempts to retrieve strongly-typed data associated with this event.
    /// This method provides safe access to stored event data without throwing exceptions
    /// for missing keys or type mismatches. This is the recommended approach when the
    /// existence or type of data is uncertain.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <param name="value">When this method returns, contains the retrieved value if successful,
    /// or the default value for T if unsuccessful.</param>
    /// <returns>true if the data was found and successfully cast to the specified type; otherwise, false.</returns>
    /// <example>
    /// <code>
    /// // Safe data retrieval with error handling
    /// if (myEvent.TryGetData&lt;string&gt;("user.name", out var userName)) {
    ///     // Use userName safely - we know it exists and is the correct type
    ///     Console.WriteLine($"Processing event for user: {userName}");
    /// } else {
    ///     // Handle missing or invalid data appropriately
    ///     Console.WriteLine("User name not available or invalid type");
    /// }
    /// 
    /// // Using with default values
    /// var retryCount = myEvent.TryGetData&lt;int&gt;("retry.count", out var count) ? count : 0;
    /// </code>
    /// </example>
    bool TryGetData<T>(string key, out T value);
}
