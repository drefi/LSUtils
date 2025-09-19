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

    public LSEventProcessStatus Process(LSEventContextManager? contextManager = null) {
        var manager = contextManager ?? LSEventContextManager.Singleton;
        if (_processContext != null) throw new LSException("Event already processed.");

        var globalContextBuilder = new LSEventContextBuilder(manager.getContext(this.GetType()));
        if (_eventContext != null) {
            globalContextBuilder
                .Merge(_eventContext);
                

        }
        _processContext = new LSEventProcessContext(this, globalContextBuilder.Build());
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

    public ILSEvent Context(LSEventSubContextBuilder builder) {
        LSEventContextBuilder contextBuilder;
        if (_eventContext == null) {
            contextBuilder = new LSEventContextBuilder();
        } else {
            contextBuilder = new LSEventContextBuilder(_eventContext);
        }
        _eventContext = builder(contextBuilder).Build();
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
