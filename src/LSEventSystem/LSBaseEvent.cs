using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Abstract base class that provides a complete implementation of the event system's core functionality.
/// Serves as the foundation for all events in the system, providing data storage, state tracking, 
/// and integration with the event processing pipeline.
/// 
/// Inherit from this class when creating events that don't need a specific source instance.
/// For events tied to a specific object, consider using LSEvent&lt;TInstance&gt; instead.
/// 
/// Supports both global handlers via dispatcher and event-scoped handlers via WithCallbacks().
/// </summary>
public abstract class LSBaseEvent : ILSMutableEvent {
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
    /// Indicates if the event is currently waiting for an async operation to complete.
    /// </summary>
    public bool IsWaiting { get; set; }

    /// <summary>
    /// Tracks deferred completion state when Resume/Abort/Fail is called before IsWaiting is set.
    /// </summary>
    private ResumptionType? _deferredResumption = null;

    /// <summary>
    /// Reference to the dispatcher processing this event (used for resumption).
    /// </summary>
    public LSDispatcher Dispatcher { get; protected set; } = LSDispatcher.Singleton;

    /// <summary>
    /// Indicates whether this event has been built with a callback builder.
    /// </summary>
    public bool IsBuilt { get; private set; }

    /// <summary>
    /// Optional error message if the event encountered an error.
    /// </summary>
    public string? ErrorMessage { get; internal set; }

    /// <summary>
    /// Read-only access to event data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data => _data;

    public bool InDispatch => throw new NotImplementedException();

    /// <summary>
    /// Initializes a new event instance.
    /// </summary>
    protected LSBaseEvent() {
    }

    /// <summary>
    /// Sets data associated with this event.
    /// </summary>
    /// <param name="key">The key to store the data under.</param>
    /// <param name="value">The value to store.</param>
    public void SetData<T>(string key, T value) => _data[key] = value!;

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
    /// Thread-safe method that notifies the dispatcher to continue processing.
    /// </summary>
    public void Resume() {
        continueProcessingWith(ResumptionType.Resume);
    }

    /// <summary>
    /// Signals that an async operation has failed and event processing should be cancelled.
    /// Thread-safe method that cancels the event and proceeds to the CANCEL phase.
    /// </summary>
    public void Abort() {
        continueProcessingWith(ResumptionType.Abort);
    }

    /// <summary>
    /// Signals that an async operation has failed but event processing should continue.
    /// Thread-safe method that marks the event as having failures and proceeds to the FAILURE phase.
    /// </summary>
    public void Fail() {
        continueProcessingWith(ResumptionType.Fail);
    }

    /// <summary>
    /// Internal enumeration for different types of resumption from WAITING state.
    /// </summary>
    internal enum ResumptionType {
        Resume,  // Continue normally
        Abort,   // Cancel the event
        Fail     // Mark as failed but continue
    }

    /// <summary>
    /// Checks if there's a deferred resumption pending and returns it.
    /// This is used by the dispatcher to handle scenario 2 (immediate completion).
    /// </summary>
    internal ResumptionType? GetDeferredResumption() {
        return _deferredResumption;
    }

    /// <summary>
    /// Clears the deferred resumption state after it has been processed.
    /// </summary>
    internal void ClearDeferredResumption() {
        _deferredResumption = null;
    }

    /// <summary>
    /// Common implementation for Resume, Abort, and Fail methods.
    /// Handles both immediate processing (when IsWaiting = true) and deferred processing (when IsWaiting = false).
    /// </summary>
    /// <param name="resumptionType">The type of resumption to perform.</param>
    private void continueProcessingWith(ResumptionType resumptionType) {
        lock (_data) { // Use existing lock for thread safety
            if (IsWaiting) {
                // Scenario 1: Normal async completion - event is already waiting
                IsWaiting = false;

                // Set appropriate outcome state
                switch (resumptionType) {
                    case ResumptionType.Resume:
                        // No additional state changes needed
                        break;
                    case ResumptionType.Abort:
                        IsCancelled = true;
                        break;
                    case ResumptionType.Fail:
                        HasFailures = true;
                        break;
                }

                // Resume processing through the dispatcher
                Dispatcher.continueProcessing(this);
            } else {
                // Scenario 2: Immediate completion - event not yet waiting, defer the action
                _deferredResumption = resumptionType;

                // Apply state changes immediately for immediate effect
                switch (resumptionType) {
                    case ResumptionType.Resume:
                        // No additional state changes needed
                        break;
                    case ResumptionType.Abort:
                        IsCancelled = true;
                        break;
                    case ResumptionType.Fail:
                        HasFailures = true;
                        break;
                }

                // Note: No dispatcher call here - the dispatcher will check for deferred resumption
            }
        }
    }

    /// <summary>
    /// Creates a callback builder for registering event-specific handlers that execute only for this event instance.
    /// Provides a fluent API for setting up one-time handlers with automatic cleanup.
    /// </summary>
    /// <typeparam name="TEventType">The concrete event type for type-safe handler registration.</typeparam>
    /// <param name="dispatcher">The dispatcher to register handlers with.</param>
    /// <returns>A callback builder that allows fluent configuration of event-specific handlers.</returns>
    public LSEventCallbackBuilder<TEventType> WithCallbacks<TEventType>(LSDispatcher dispatcher)
        where TEventType : ILSEvent {
        if (IsBuilt) throw new LSException("Event has already been built.");

        Dispatcher = dispatcher;
        IsBuilt = true;
        return new LSEventCallbackBuilder<TEventType>((TEventType)(object)this, Dispatcher);
    }

    /// <summary>
    /// Dispatches this event directly through the specified dispatcher without event-scoped callbacks.
    /// This is the preferred method when you only need global handlers to process the event.
    /// 
    /// This method provides a clean, direct API for event processing when no event-specific handlers
    /// are required. It automatically handles event lifecycle and integrates with the global handler
    /// system registered on the dispatcher.
    /// 
    /// For events that need event-specific handlers, use WithCallbacks() instead.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to process this event with. Must not be null.</param>
    /// <returns>
    /// True if the event completed successfully through all phases, 
    /// false if it was cancelled, had critical failures, or is waiting for async operations.
    /// </returns>
    /// <exception cref="LSException">Thrown if the event has already been built or dispatched.</exception>
    /// <exception cref="ArgumentNullException">Thrown if dispatcher is null.</exception>
    public bool Dispatch(LSDispatcher dispatcher) {
        if (dispatcher == null)
            throw new ArgumentNullException(nameof(dispatcher), "Dispatcher cannot be null");
        if (IsBuilt)
            throw new LSException("Event has already been built. Use Dispatch() without parameters or create a new event.");

        Dispatcher = dispatcher;
        IsBuilt = true;

        return dispatcher.processEvent(this);
    }

    /// <summary>
    /// Dispatches this event through the singleton dispatcher without event-scoped callbacks.
    /// This is a convenience method that uses the default singleton dispatcher for simple scenarios.
    /// 
    /// Equivalent to calling Dispatch(LSDispatcher.Singleton). Use this when you don't need
    /// a custom dispatcher configuration and want the simplest possible event processing.
    /// </summary>
    /// <returns>
    /// True if the event completed successfully through all phases,
    /// false if it was cancelled, had critical failures, or is waiting for async operations.
    /// </returns>
    /// <exception cref="LSException">Thrown if the event has already been built or dispatched.</exception>
    public bool Dispatch() {
        return Dispatch(LSDispatcher.Singleton);
    }

    EventProcessResult ILSEvent.Dispatch() {
        throw new NotImplementedException();
    }
}
