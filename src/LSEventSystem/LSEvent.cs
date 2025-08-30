using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Generic event base class that provides strongly-typed access to the source object that triggered the event.
/// This class extends LSBaseEvent with type-safe instance access and is the recommended foundation
/// for most domain events that are associated with a specific entity or object.
/// </summary>
/// <typeparam name="TInstance">
/// The type of the object that triggered this event. Must be a reference type (class).
/// This provides compile-time type safety and eliminates the need for casting in event handlers.
/// </typeparam>
public abstract class LSEvent<TInstance> : LSBaseEvent where TInstance : class {
    /// <summary>
    /// The strongly-typed instance that triggered this event.
    /// This property provides direct, type-safe access to the source object without casting.
    /// The reference remains constant throughout the event's lifecycle.
    /// </summary>
    public TInstance Instance { get; }

    /// <summary>
    /// Initializes a new event with the specified source instance.
    /// The instance becomes immutably associated with this event and cannot be changed after construction.
    /// </summary>
    /// <param name="instance">
    /// The instance that triggered this event. Must not be null.
    /// This object will be available to all event handlers through the Instance property.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when instance is null. All events must have a valid source instance.
    /// </exception>
    protected LSEvent(TInstance instance) {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance), 
            $"The source instance for event type {GetType().Name} cannot be null.");
    }
}
