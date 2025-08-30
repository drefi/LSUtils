using LSUtils.EventSystem;

namespace LSUtils;

/// <summary>
/// Interface for entities that can participate in the event system.
/// Provides standardized lifecycle management and integration with event processing.
/// </summary>
public interface ILSEventable : ILSClass {
    /// <summary>
    /// Gets the unique identifier for this eventable entity.
    /// </summary>
    System.Guid ID { get; }

    /// <summary>
    /// Gets a value indicating whether this eventable has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes the eventable with the specified options.
    /// This method sets up the entity for participation in event processing.
    /// </summary>
    /// <param name="options">The options for initialization, including dispatcher and owner instance.</param>
    void Initialize(LSEventOptions options);

    /// <summary>
    /// Performs cleanup operations for this eventable.
    /// This method should release resources and unregister from event processing.
    /// </summary>
    void Cleanup();
}
