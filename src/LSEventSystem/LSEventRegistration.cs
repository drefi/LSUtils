using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Registration builder for fluent API that allows configuring event handlers
/// with various options before registering them with the dispatcher.
/// This provides a clean, readable way to set up complex handler configurations.
/// </summary>
/// <typeparam name="TEvent">The event type this registration is for.</typeparam>
public class LSEventRegistration<TEvent> where TEvent : ILSEvent {
    private readonly LSDispatcher _dispatcher;
    private LSEventPhase _phase = LSEventPhase.EXECUTE;
    private LSPhasePriority _priority = LSPhasePriority.NORMAL;
    private Type? _instanceType = null;
    private object? _instance = null;
    private int _maxExecutions = -1;
    private Func<TEvent, bool>? _condition = null;
    
    /// <summary>
    /// Initializes a new registration builder for the specified dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to register the handler with.</param>
    internal LSEventRegistration(LSDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }
    
    /// <summary>
    /// Specifies which phase this handler should execute in.
    /// </summary>
    /// <param name="phase">The execution phase.</param>
    /// <returns>This registration builder for fluent chaining.</returns>
    public LSEventRegistration<TEvent> InPhase(LSEventPhase phase) {
        _phase = phase;
        return this;
    }
    
    /// <summary>
    /// Specifies the execution priority within the phase.
    /// Handlers with higher priority (lower numeric value) execute first.
    /// </summary>
    /// <param name="priority">The execution priority.</param>
    /// <returns>This registration builder for fluent chaining.</returns>
    public LSEventRegistration<TEvent> WithPriority(LSPhasePriority priority) {
        _priority = priority;
        return this;
    }
    
    /// <summary>
    /// Restricts this handler to only execute for events from a specific instance.
    /// Useful for instance-specific event handling.
    /// </summary>
    /// <typeparam name="T">The type of the instance.</typeparam>
    /// <param name="instance">The specific instance this handler should respond to.</param>
    /// <returns>This registration builder for fluent chaining.</returns>
    public LSEventRegistration<TEvent> ForInstance<T>(T instance) where T : class {
        _instanceType = typeof(T);
        _instance = instance;
        return this;
    }
    
    /// <summary>
    /// Limits the maximum number of times this handler can execute.
    /// Useful for one-time handlers or handlers with limited executions.
    /// </summary>
    /// <param name="count">The maximum number of executions allowed. Use -1 for unlimited.</param>
    /// <returns>This registration builder for fluent chaining.</returns>
    public LSEventRegistration<TEvent> MaxExecutions(int count) {
        _maxExecutions = count;
        return this;
    }
    
    /// <summary>
    /// Adds a condition that must be met for this handler to execute.
    /// The handler will only be called if the condition returns true.
    /// </summary>
    /// <param name="condition">A function that determines if the handler should execute.</param>
    /// <returns>This registration builder for fluent chaining.</returns>
    public LSEventRegistration<TEvent> When(Func<TEvent, bool> condition) {
        _condition = condition;
        return this;
    }
    
    /// <summary>
    /// Registers the handler with the configured options.
    /// </summary>
    /// <param name="handler">The handler function to register.</param>
    /// <returns>A unique identifier for the registered handler that can be used for unregistration.</returns>
    public Guid Register(LSPhaseHandler<TEvent> handler) {
        return _dispatcher.RegisterHandler(handler, _phase, _priority, _instanceType, _instance, _maxExecutions, _condition);
    }
}
