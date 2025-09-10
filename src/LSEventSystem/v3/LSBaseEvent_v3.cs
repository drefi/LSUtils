using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// V3 base event class that provides integration with the LSDispatcher_v3 nested callback API.
/// This version supports the same BehaviourTreeBuilder-style fluent API for event-scoped handlers
/// that are automatically cleaned up after event processing.
/// 
/// Version 3 fixes:
/// - Properly combines global and event-scoped handlers
/// - Preserves registration order
/// - Eliminates temporary dispatcher issues
/// 
/// Inherit from this class when creating events for use with the v3 event system.
/// </summary>
public abstract class LSBaseEvent_v3 : ILSEvent {
    private readonly ConcurrentDictionary<string, object> _data = new();

    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public System.Guid ID { get; } = System.Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when this event was created.
    /// </summary>
    public System.DateTime CreatedAt { get; } = System.DateTime.UtcNow;

    /// <summary>
    /// Indicates if the event processing was cancelled.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Indicates if the event has failures but processing can continue.
    /// </summary>
    public bool HasFailures { get; set; }

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
    /// Indicates whether this event has been built with a callback builder.
    /// </summary>
    public bool IsBuilt { get; private set; }

    /// <summary>
    /// Read-only access to event data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data => _data;

    public bool InDispatch => throw new NotImplementedException();

    /// <summary>
    /// Initializes a new event instance.
    /// </summary>
    protected LSBaseEvent_v3() {
    }

    /// <summary>
    /// Sets data associated with this event.
    /// </summary>
    /// <param name="key">The key to store the data under.</param>
    /// <param name="value">The value to store.</param>
    public void SetData<T>(string key, T value) => _data[key] = value!;

    /// <summary>
    /// Gets strongly-typed data associated with this event.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="key">The key to retrieve data for.</param>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found.</exception>
    /// <exception cref="InvalidCastException">Thrown when the data cannot be cast to the specified type.</exception>
    public virtual T GetData<T>(string key) => (T)_data[key];

    /// <summary>
    /// Attempts to get strongly-typed data associated with this event.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="key">The key to retrieve data for.</param>
    /// <param name="value">The retrieved value if successful.</param>
    /// <returns>True if the data was found and successfully cast to the specified type.</returns>
    public virtual bool TryGetData<T>(string key, out T value) {
        if (_data.TryGetValue(key, out var obj) && obj is T typed) {
            value = typed;
            return true;
        }
        value = default(T)!;
        return false;
    }

    /// <summary>
    /// Signals that an async operation has completed and event processing should resume.
    /// </summary>
    public void Resume() {
        // Future implementation for async support
    }

    /// <summary>
    /// Signals that an async operation has failed and event processing should be cancelled.
    /// </summary>
    public void Abort() {
        IsCancelled = true;
    }

    /// <summary>
    /// Signals that an async operation has failed but event processing should continue.
    /// </summary>
    public void Fail() {
        HasFailures = true;
    }

    /// <summary>
    /// Creates a callback builder for registering event-specific handlers that execute only for this event instance.
    /// Provides the same BehaviourTreeBuilder-style nested callback API as the v3 dispatcher but for event-scoped handlers.
    /// 
    /// FIXED: Now properly combines global and event-scoped handlers.
    /// </summary>
    /// <typeparam name="TEventType">The concrete event type for type-safe handler registration.</typeparam>
    /// <param name="dispatcher">The v3 dispatcher to process this event with.</param>
    /// <returns>A callback builder that allows fluent configuration of event-specific handlers.</returns>
    public LSEventCallbackBuilder_v3<TEventType> WithCallbacks<TEventType>(LSDispatcher_v3 dispatcher)
        where TEventType : ILSEvent {
        if (IsBuilt) {
            throw new InvalidOperationException("This event has already been built with callbacks. Each event can only be built once.");
        }

        if (dispatcher == null) {
            throw new ArgumentNullException(nameof(dispatcher));
        }

        IsBuilt = true;
        return new LSEventCallbackBuilder_v3<TEventType>((TEventType)(object)this, dispatcher);
    }

    /// <summary>
    /// Creates a callback builder using the singleton v3 dispatcher.
    /// This is a convenience method for simple scenarios.
    /// </summary>
    /// <typeparam name="TEventType">The concrete event type for type-safe handler registration.</typeparam>
    /// <returns>A callback builder that allows fluent configuration of event-specific handlers.</returns>
    public LSEventCallbackBuilder_v3<TEventType> WithCallbacks<TEventType>()
        where TEventType : ILSEvent {
        return WithCallbacks<TEventType>(LSDispatcher_v3.Singleton);
    }

    /// <summary>
    /// Dispatches this event directly through the v3 dispatcher without event-scoped callbacks.
    /// This is the preferred method when you only need global handlers to process the event.
    /// </summary>
    /// <param name="dispatcher">The v3 dispatcher to process this event with. Must not be null.</param>
    /// <returns>
    /// True if the event completed successfully through all phases, 
    /// false if it was cancelled, had critical failures, or is waiting for async operations.
    /// </returns>
    /// <exception cref="LSException">Thrown if the event has already been built or dispatched.</exception>
    /// <exception cref="ArgumentNullException">Thrown if dispatcher is null.</exception>
    public bool Dispatch(LSDispatcher_v3 dispatcher) {
        if (dispatcher == null) {
            throw new ArgumentNullException(nameof(dispatcher));
        }

        if (IsBuilt) {
            throw new InvalidOperationException("This event has already been built with callbacks. Use the builder's Dispatch() method instead.");
        }

        IsBuilt = true;
        var globalHandlers = dispatcher.getHandlersForEvent(GetType());
        return dispatcher.processEventWithHandlers(this, globalHandlers);
    }

    /// <summary>
    /// Dispatches this event through the singleton v3 dispatcher without event-scoped callbacks.
    /// This is a convenience method that uses the default singleton dispatcher for simple scenarios.
    /// </summary>
    /// <returns>
    /// True if the event completed successfully through all phases,
    /// false if it was cancelled, had critical failures, or is waiting for async operations.
    /// </returns>
    /// <exception cref="LSException">Thrown if the event has already been built or dispatched.</exception>
    public bool Dispatch() {
        return Dispatch(LSDispatcher_v3.Singleton);
    }

    EventProcessResult ILSEvent.Dispatch() {
        throw new NotImplementedException();
    }
}
