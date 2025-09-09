using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// V4 base event class with clean state machine and simplified design.
/// Provides integration with LSDispatcher_v4 and state-based processing.
/// </summary>
public abstract class BaseEvent : ILSEvent {
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
    /// Current phase being executed (only relevant in Business state).
    /// </summary>
    public EventSystemPhase CurrentPhase { get; internal set; } = EventSystemPhase.VALIDATE;

    /// <summary>
    /// Phases that have been completed successfully.
    /// </summary>
    public EventSystemPhase CompletedPhases { get; internal set; }

    /// <summary>
    /// Indicates if the event processing was cancelled.
    /// </summary>
    public bool IsCancelled { get; internal set; }

    /// <summary>
    /// Indicates if the event has failures but processing can continue.
    /// In v4, this is determined by phase results rather than a separate flag.
    /// </summary>
    public bool HasFailures { get; internal set; }

    /// <summary>
    /// Indicates if the event has completed processing.
    /// </summary>
    public bool IsCompleted { get; internal set; }

    /// <summary>
    /// Read-only access to event data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data => _data;

    LSEventPhase ILSEvent.CurrentPhase => throw new NotImplementedException();

    LSEventPhase ILSEvent.CompletedPhases => throw new NotImplementedException();

    /// <summary>
    /// Stores data associated with this event.
    /// </summary>
    public virtual void SetData<T>(string key, T value) {
        _data[key] = (object)value!;
    }

    /// <summary>
    /// Retrieves data associated with this event.
    /// </summary>
    public virtual T GetData<T>(string key) {
        if (_data.TryGetValue(key, out var value) && value is T typedValue) {
            return typedValue;
        }
        return default(T)!;
    }

    /// <summary>
    /// Tries to retrieve data associated with this event.
    /// </summary>
    public virtual bool TryGetData<T>(string key, out T value) {
        if (_data.TryGetValue(key, out var obj) && obj is T typedValue) {
            value = typedValue;
            return true;
        }
        value = default(T)!;
        return false;
    }

    /// <summary>
    /// Dispatches this event through the specified dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to process the event with.</param>
    /// <returns>True if the event completed successfully, false if cancelled or failed.</returns>
    public StateProcessResult Dispatch(LSESDispatcher dispatcher) {
        if (dispatcher == null) {
            throw new ArgumentNullException(nameof(dispatcher));
        }
        List<IHandlerEntry> handlers = dispatcher.getHandlers(this.GetType());

        return dispatcher.processEvent(this, handlers);
    }

    /// <summary>
    /// Dispatches this event through the singleton dispatcher.
    /// </summary>
    /// <returns>True if the event completed successfully, false if cancelled or failed.</returns>
    public StateProcessResult Dispatch() {
        return Dispatch(LSESDispatcher.Singleton);
    }

    public void WithCallbacks() {

    }

}
