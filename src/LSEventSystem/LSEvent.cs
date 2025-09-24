using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem;

public abstract class LSEvent : ILSEvent {
    private LSEventProcessContext? _processContext;
    private Dictionary<string, object> _data = new();
    private ILSEventLayerNode? _eventContext;
    public Guid ID { get; }

    public DateTime CreatedAt { get; }

    public bool IsCancelled { get; protected set; }

    public bool HasFailures { get; protected set; }

    public bool IsCompleted { get; protected set; }
    protected LSEvent() {

        ID = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public virtual T GetData<T>(string key) {
        if (_data.TryGetValue(key, out var value)) {
            if (value is T tValue) {
                return tValue;
            }
            throw new InvalidCastException($"Stored data with key '{key}' is not of type {typeof(T).FullName}.");
        }
        throw new KeyNotFoundException($"No data found for key '{key}'.");
    }

    public LSEventProcessStatus Process(ILSEventable? instance = null, LSEventContextManager? contextManager = null) {
        var manager = contextManager ?? LSEventContextManager.Singleton;
        if (_processContext != null) throw new LSException("Event already processed.");

        var globalContext = manager.GetContext(this.GetType(), instance, _eventContext);
        _processContext = new LSEventProcessContext(this, globalContext);
        return _processContext.Process();
    }

    public LSEventProcessStatus Resume(params string[] nodeIDs) {
        if (_processContext == null) {
            throw new LSException("Event not yet processed.");
        }
        return _processContext.Resume(nodeIDs);
    }

    public void Cancel() {
        if (_processContext == null) {
            throw new LSException("Event not yet processed.");
        }
        _processContext.Cancel();
    }

    // Allows building or extending the event context using a builder delegate.
    // If no existing context, starts a new parallel node with the event ID as name.
    public ILSEvent Context(LSEventContextDelegate builder, ILSEventable? instance = null) {
        LSEventContextBuilder eventBuilder;
        if (_eventContext == null) {
            // no existing context; start a new parallel node with the event ID as name.
            // creating a parallel node to allow to use handlers in the builder directly.
            // the eventContext will be merged on globalContext.
            // if an instance is provided, we use its ID as name for the root node.

            string id = instance?.ID.ToString() ?? ID.ToString();
            eventBuilder = new LSEventContextBuilder().Parallel($"{GetType().Name}");
        } else {
            eventBuilder = new LSEventContextBuilder(_eventContext);
        }
        // use the builder to modify or extend the eventContext.
        _eventContext = builder(eventBuilder).Build();
        return this;
    }

    public LSEventProcessStatus Fail(params string[] nodeIDs) {
        if (_processContext == null) {
            throw new LSException("Event not yet processed.");
        }
        return _processContext.Fail(nodeIDs);
    }
    public virtual void SetData<T>(string key, T value) {
        _data[key] = value!;
    }

    public virtual bool TryGetData<T>(string key, out T value) {
        if (_data.TryGetValue(key, out var objValue)) {
            if (objValue is T tValue) {
                value = tValue;
                return true;
            }
        }
        value = default!;
        return false;
    }
}
