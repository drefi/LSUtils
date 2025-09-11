# LSUtils API Documentation

This document provides comprehensive API documentation for LSUtils library components.

## Table of Contents

- [LSEventSystem v4 API](#lseventsystem-v4-api)
- [Core Utilities API](#core-utilities-api)
- [Mathematical Utilities API](#mathematical-utilities-api)
- [Collections API](#collections-api)
- [Graph Algorithms API](#graph-algorithms-api)
- [Examples and Usage Patterns](#examples-and-usage-patterns)

## LSEventSystem v4 API

### Core Interfaces

#### ILSEvent

```csharp
namespace LSUtils.EventSystem;

public interface ILSEvent {
    /// <summary>Gets the unique identifier for this event instance.</summary>
    Guid ID { get; }
    
    /// <summary>Gets the timestamp when this event was created.</summary>
    DateTime CreatedAt { get; }
    
    /// <summary>Gets whether this event has been cancelled.</summary>
    bool IsCancelled { get; }
    
    /// <summary>Gets whether this event has any failures.</summary>
    bool HasFailures { get; }
    
    /// <summary>Gets whether this event has completed processing.</summary>
    bool IsCompleted { get; }
    
    /// <summary>Gets whether this event is currently being dispatched.</summary>
    bool InDispatch { get; }
    
    /// <summary>Gets read-only access to event data.</summary>
    IReadOnlyDictionary<string, object> Data { get; }
    
    /// <summary>Sets data for the event with the specified key.</summary>
    /// <typeparam name="T">The type of data to store.</typeparam>
    /// <param name="key">The key to associate with the data.</param>
    /// <param name="value">The data value to store.</param>
    void SetData<T>(string key, T value);
    
    /// <summary>Gets data from the event with the specified key.</summary>
    /// <typeparam name="T">The type of data to retrieve.</typeparam>
    /// <param name="key">The key associated with the data.</param>
    /// <returns>The data value if found; otherwise, default(T).</returns>
    T GetData<T>(string key);
    
    /// <summary>Attempts to get data from the event with the specified key.</summary>
    /// <typeparam name="T">The type of data to retrieve.</typeparam>
    /// <param name="key">The key associated with the data.</param>
    /// <param name="value">When this method returns, contains the data value if found; otherwise, default(T).</param>
    /// <returns>true if the data was found; otherwise, false.</returns>
    bool TryGetData<T>(string key, out T value);
    
    /// <summary>Dispatches this event for processing.</summary>
    /// <returns>The result of event processing.</returns>
    EventProcessResult Dispatch();
}
```

#### ILSEventable

```csharp
namespace LSUtils.EventSystem;

public interface ILSEventable {
    /// <summary>Gets the dispatcher associated with this eventable object.</summary>
    LSDispatcher? Dispatcher { get; }
    
    /// <summary>Initializes this eventable object with the specified options.</summary>
    /// <param name="options">The event options containing dispatcher and configuration.</param>
    void Initialize(LSEventOptions options);
}
```

#### IEventProcessState

```csharp
namespace LSUtils.EventSystem;

public interface IEventProcessState {
    /// <summary>Gets the result of state processing.</summary>
    StateProcessResult StateResult { get; }
    
    /// <summary>Gets whether the state has any failures.</summary>
    bool HasFailures { get; }
    
    /// <summary>Gets whether the state has been cancelled.</summary>
    bool HasCancelled { get; }
    
    /// <summary>Processes the current state.</summary>
    /// <returns>The next state to transition to, or null if processing is complete.</returns>
    IEventProcessState? Process();
    
    /// <summary>Resumes processing from this state.</summary>
    /// <returns>The next state to transition to, or null if processing is complete.</returns>
    IEventProcessState? Resume();
    
    /// <summary>Cancels processing at this state.</summary>
    /// <returns>The cancellation state.</returns>
    IEventProcessState? Cancel();
    
    /// <summary>Marks processing as failed at this state.</summary>
    /// <returns>The next appropriate state based on the failure.</returns>
    IEventProcessState? Fail();
}
```

### Core Classes

#### LSEvent

```csharp
namespace LSUtils.EventSystem;

public abstract class LSEvent : ILSEvent {
    /// <summary>Initializes a new instance of the LSEvent class.</summary>
    /// <param name="options">The event options containing configuration.</param>
    protected LSEvent(LSEventOptions? options = null);
    
    /// <summary>Gets the unique identifier for this event instance.</summary>
    public Guid ID { get; }
    
    /// <summary>Gets the timestamp when this event was created.</summary>
    public DateTime CreatedAt { get; }
    
    /// <summary>Gets whether this event has been cancelled.</summary>
    public bool IsCancelled { get; protected set; }
    
    /// <summary>Gets whether this event has any failures.</summary>
    public bool HasFailures { get; protected set; }
    
    /// <summary>Gets whether this event has completed processing.</summary>
    public bool IsCompleted { get; protected set; }
    
    /// <summary>Gets whether this event is currently being dispatched.</summary>
    public bool InDispatch { get; protected set; }
    
    /// <summary>Gets read-only access to event data.</summary>
    public IReadOnlyDictionary<string, object> Data { get; }
    
    /// <summary>Dispatches this event for processing through the state machine.</summary>
    /// <returns>The result of event processing.</returns>
    public EventProcessResult Dispatch();
    
    /// <summary>Adds phase-specific callbacks to this event.</summary>
    /// <typeparam name="TPhase">The phase type to add callbacks for.</typeparam>
    /// <param name="configure">Function to configure the phase handler.</param>
    /// <returns>This event instance for method chaining.</returns>
    public LSEvent WithPhaseCallbacks<TPhase>(
        Func<LSPhaseHandlerRegister<TPhase>, LSPhaseHandlerRegister<TPhase>> configure) 
        where TPhase : BusinessState.PhaseState;
    
    /// <summary>Adds state-specific callbacks to this event.</summary>
    /// <typeparam name="TState">The state type to add callbacks for.</typeparam>
    /// <param name="configure">Function to configure the state handler.</param>
    /// <returns>This event instance for method chaining.</returns>
    public LSEvent WithStateCallbacks<TState>(
        Func<LSStateHandlerRegister<TState>, LSStateHandlerRegister<TState>> configure) 
        where TState : IEventProcessState;
    
    /// <summary>Adds a callback to execute when the event completes successfully.</summary>
    /// <param name="callback">The callback to execute on completion.</param>
    /// <returns>This event instance for method chaining.</returns>
    public LSEvent OnCompleted(LSAction<ILSEvent> callback);
    
    /// <summary>Adds a callback to execute when the event is cancelled.</summary>
    /// <param name="callback">The callback to execute on cancellation.</param>
    /// <returns>This event instance for method chaining.</returns>
    public LSEvent OnCancelled(LSAction<ILSEvent> callback);
    
    /// <summary>Adds a callback to execute when the event succeeds.</summary>
    /// <param name="callback">The callback to execute on success.</param>
    /// <returns>This event instance for method chaining.</returns>
    public LSEvent OnSuccess(LSAction<ILSEvent> callback);
}
```

#### LSEvent<T>

```csharp
namespace LSUtils.EventSystem;

public abstract class LSEvent<T> : LSEvent {
    /// <summary>Initializes a new instance of the LSEvent&lt;T&gt; class.</summary>
    /// <param name="instance">The instance associated with this event.</param>
    /// <param name="options">The event options containing configuration.</param>
    protected LSEvent(T instance, LSEventOptions? options = null) : base(options);
    
    /// <summary>Gets the instance associated with this event.</summary>
    public T Instance { get; }
}
```

#### LSEventOptions

```csharp
namespace LSUtils.EventSystem;

public class LSEventOptions {
    /// <summary>Initializes a new instance of the LSEventOptions class.</summary>
    /// <param name="dispatcher">The dispatcher to use for event processing.</param>
    /// <param name="ownerInstance">The instance that owns this event.</param>
    public LSEventOptions(LSDispatcher? dispatcher = null, object? ownerInstance = null);
    
    /// <summary>Gets the dispatcher for event processing.</summary>
    public LSDispatcher Dispatcher { get; init; }
    
    /// <summary>Gets the instance that owns this event.</summary>
    public object? OwnerInstance { get; protected set; }
    
    /// <summary>Adds a success callback to be executed when events succeed.</summary>
    /// <param name="callback">Function to configure the success handler.</param>
    /// <returns>This options instance for method chaining.</returns>
    public LSEventOptions OnSuccess(
        Func<LSStateHandlerRegister<SucceedState>, LSStateHandlerRegister<SucceedState>> callback);
    
    /// <summary>Adds a cancellation callback to be executed when events are cancelled.</summary>
    /// <param name="callback">Function to configure the cancellation handler.</param>
    /// <returns>This options instance for method chaining.</returns>
    public LSEventOptions OnCancel(
        Func<LSStateHandlerRegister<CancelledState>, LSStateHandlerRegister<CancelledState>> callback);
    
    /// <summary>Adds a completion callback to be executed when events complete.</summary>
    /// <param name="callback">Function to configure the completion handler.</param>
    /// <returns>This options instance for method chaining.</returns>
    public LSEventOptions OnComplete(
        Func<LSStateHandlerRegister<CompletedState>, LSStateHandlerRegister<CompletedState>> callback);
    
    /// <summary>Adds a validate phase callback.</summary>
    /// <param name="callback">Function to configure the validate phase handler.</param>
    /// <returns>This options instance for method chaining.</returns>
    public LSEventOptions OnValidatePhase(
        Func<LSPhaseHandlerRegister<BusinessState.ValidatePhaseState>, 
             LSPhaseHandlerRegister<BusinessState.ValidatePhaseState>> callback);
    
    /// <summary>Adds a configure phase callback.</summary>
    /// <param name="callback">Function to configure the configure phase handler.</param>
    /// <returns>This options instance for method chaining.</returns>
    public LSEventOptions OnConfigurePhase(
        Func<LSPhaseHandlerRegister<BusinessState.ConfigurePhaseState>, 
             LSPhaseHandlerRegister<BusinessState.ConfigurePhaseState>> callback);
    
    /// <summary>Adds an execute phase callback.</summary>
    /// <param name="callback">Function to configure the execute phase handler.</param>
    /// <returns>This options instance for method chaining.</returns>
    public LSEventOptions OnExecutePhase(
        Func<LSPhaseHandlerRegister<BusinessState.ExecutePhaseState>, 
             LSPhaseHandlerRegister<BusinessState.ExecutePhaseState>> callback);
}
```

#### LSDispatcher

```csharp
namespace LSUtils.EventSystem;

public class LSDispatcher {
    /// <summary>Gets the singleton instance of the dispatcher.</summary>
    public static LSDispatcher Singleton { get; }
    
    /// <summary>Registers multiple handlers for an event type.</summary>
    /// <typeparam name="TEvent">The event type to register handlers for.</typeparam>
    /// <param name="configureRegister">Function to configure the event register.</param>
    /// <returns>Array of handler IDs that were registered.</returns>
    public Guid[] ForEvent<TEvent>(
        Func<LSEventRegister<TEvent>, Guid[]> configureRegister) 
        where TEvent : ILSEvent;
    
    /// <summary>Registers a handler for a specific event phase.</summary>
    /// <typeparam name="TEvent">The event type to register the handler for.</typeparam>
    /// <typeparam name="TPhase">The phase type to register the handler for.</typeparam>
    /// <param name="configurePhaseHandler">Function to configure the phase handler.</param>
    /// <returns>The ID of the registered handler.</returns>
    public Guid ForEventPhase<TEvent, TPhase>(
        Func<LSPhaseHandlerRegister<TPhase>, LSPhaseHandlerRegister<TPhase>> configurePhaseHandler) 
        where TEvent : ILSEvent 
        where TPhase : BusinessState.PhaseState;
    
    /// <summary>Registers a handler for a specific event state.</summary>
    /// <typeparam name="TEvent">The event type to register the handler for.</typeparam>
    /// <typeparam name="TState">The state type to register the handler for.</typeparam>
    /// <param name="configureStateHandler">Function to configure the state handler.</param>
    /// <returns>The ID of the registered handler.</returns>
    public Guid ForEventState<TEvent, TState>(
        Func<LSStateHandlerRegister<TState>, LSStateHandlerRegister<TState>> configureStateHandler) 
        where TEvent : ILSEvent 
        where TState : IEventProcessState;
}
```

### Handler Registration Classes

#### LSPhaseHandlerRegister<TPhase>

```csharp
namespace LSUtils.EventSystem;

public class LSPhaseHandlerRegister<TPhase> where TPhase : BusinessState.PhaseState {
    /// <summary>Sets the execution priority for this handler within its phase.</summary>
    /// <param name="priority">The priority level for handler execution.</param>
    /// <returns>This register instance for method chaining.</returns>
    public LSPhaseHandlerRegister<TPhase> WithPriority(LSPriority priority);
    
    /// <summary>Adds a condition that must be met for the handler to execute.</summary>
    /// <param name="condition">Function that returns true if the handler should execute.</param>
    /// <returns>This register instance for method chaining.</returns>
    public LSPhaseHandlerRegister<TPhase> When(Func<ILSEvent, IHandlerEntry, bool> condition);
    
    /// <summary>Sets the main handler function that executes during the phase.</summary>
    /// <param name="handler">The handler function to execute.</param>
    /// <returns>This register instance for method chaining.</returns>
    public LSPhaseHandlerRegister<TPhase> Handler(
        Func<LSEventProcessContext, HandlerProcessResult> handler);
    
    /// <summary>Creates a handler that cancels the event if a condition is met.</summary>
    /// <param name="condition">Function that returns true if the event should be cancelled.</param>
    /// <returns>This register instance for method chaining.</returns>
    public LSPhaseHandlerRegister<TPhase> CancelIf(Func<ILSEvent, IHandlerEntry, bool> condition);
    
    /// <summary>Builds the handler entry from the current configuration.</summary>
    /// <returns>The configured handler entry.</returns>
    public LSPhaseHandlerEntry Build();
    
    /// <summary>Builds and registers the handler with the specified dispatcher.</summary>
    /// <param name="dispatcher">The dispatcher to register the handler with.</param>
    /// <returns>The ID of the registered handler.</returns>
    public Guid Register(LSDispatcher dispatcher);
}
```

#### LSStateHandlerRegister<TState>

```csharp
namespace LSUtils.EventSystem;

public class LSStateHandlerRegister<TState> where TState : IEventProcessState {
    /// <summary>Sets the execution priority for this handler within its state.</summary>
    /// <param name="priority">The priority level for handler execution.</param>
    /// <returns>This register instance for method chaining.</returns>
    public LSStateHandlerRegister<TState> WithPriority(LSPriority priority);
    
    /// <summary>Adds a condition that must be met for the handler to execute.</summary>
    /// <param name="condition">Function that returns true if the handler should execute.</param>
    /// <returns>This register instance for method chaining.</returns>
    public LSStateHandlerRegister<TState> When(Func<ILSEvent, IHandlerEntry, bool> condition);
    
    /// <summary>Sets the main handler action that executes when the event enters the state.</summary>
    /// <param name="handler">The handler action to execute.</param>
    /// <returns>This register instance for method chaining.</returns>
    public LSStateHandlerRegister<TState> Handler(LSAction<ILSEvent> handler);
    
    /// <summary>Builds the handler entry from the current configuration.</summary>
    /// <returns>The configured handler entry.</returns>
    public LSStateHandlerEntry Build();
    
    /// <summary>Builds and registers the handler with the specified dispatcher.</summary>
    /// <param name="dispatcher">The dispatcher to register the handler with.</param>
    /// <returns>The ID of the registered handler.</returns>
    public Guid Register(LSDispatcher dispatcher);
}
```

### State Classes

#### BusinessState

```csharp
namespace LSUtils.EventSystem;

public class BusinessState : IEventProcessState {
    /// <summary>Phase state for input validation and early checks.</summary>
    public class ValidatePhaseState : PhaseState { }
    
    /// <summary>Phase state for resource allocation and setup.</summary>
    public class ConfigurePhaseState : PhaseState { }
    
    /// <summary>Phase state for core business logic execution.</summary>
    public class ExecutePhaseState : PhaseState { }
    
    /// <summary>Phase state for finalization and resource cleanup.</summary>
    public class CleanupPhaseState : PhaseState { }
    
    /// <summary>Base class for all business phases.</summary>
    public abstract class PhaseState { }
}
```

#### SucceedState

```csharp
namespace LSUtils.EventSystem;

/// <summary>State representing successful completion of event processing.</summary>
public class SucceedState : IEventProcessState {
    /// <summary>Initializes a new instance of the SucceedState class.</summary>
    /// <param name="context">The event processing context.</param>
    public SucceedState(LSEventProcessContext context);
}
```

#### CancelledState

```csharp
namespace LSUtils.EventSystem;

/// <summary>State representing cancellation of event processing.</summary>
public class CancelledState : IEventProcessState {
    /// <summary>Initializes a new instance of the CancelledState class.</summary>
    /// <param name="context">The event processing context.</param>
    public CancelledState(LSEventProcessContext context);
}
```

#### CompletedState

```csharp
namespace LSUtils.EventSystem;

/// <summary>Final state for all events, regardless of success or failure.</summary>
public class CompletedState : IEventProcessState {
    /// <summary>Initializes a new instance of the CompletedState class.</summary>
    /// <param name="context">The event processing context.</param>
    public CompletedState(LSEventProcessContext context);
}
```

### Enumerations

#### EventProcessResult

```csharp
namespace LSUtils.EventSystem;

/// <summary>Represents the result of event processing.</summary>
public enum EventProcessResult {
    /// <summary>Unknown or uninitialized result.</summary>
    UNKNOWN = 0,
    
    /// <summary>Event processed successfully.</summary>
    SUCCESS = 1,
    
    /// <summary>Event processing failed but was handled gracefully.</summary>
    FAILURE = 2,
    
    /// <summary>Event processing was cancelled.</summary>
    CANCELLED = 3,
    
    /// <summary>Event is waiting for external input or async operation.</summary>
    WAITING = 4
}
```

#### HandlerProcessResult

```csharp
namespace LSUtils.EventSystem;

/// <summary>Represents the result of handler execution.</summary>
public enum HandlerProcessResult {
    /// <summary>Unknown or uninitialized result.</summary>
    UNKNOWN = 0,
    
    /// <summary>Handler executed successfully.</summary>
    SUCCESS = 1,
    
    /// <summary>Handler execution failed.</summary>
    FAILURE = 2,
    
    /// <summary>Handler cancelled the operation.</summary>
    CANCELLED = 3,
    
    /// <summary>Handler is waiting for external input.</summary>
    WAITING = 4
}
```

#### LSPriority

```csharp
namespace LSUtils.EventSystem;

/// <summary>Execution priority levels for handlers.</summary>
public enum LSPriority {
    /// <summary>Background priority - executes last.</summary>
    BACKGROUND = 0,
    
    /// <summary>Low priority.</summary>
    LOW = 1,
    
    /// <summary>Normal priority - default level.</summary>
    NORMAL = 2,
    
    /// <summary>High priority.</summary>
    HIGH = 3,
    
    /// <summary>Critical priority - executes first.</summary>
    CRITICAL = 4
}
```

## Core Utilities API

### LSSignals

```csharp
namespace LSUtils.EventSystem;

/// <summary>Provides event-based notifications for various message types.</summary>
public static class LSSignals {
    /// <summary>Sends an error message.</summary>
    /// <param name="message">The error message to send.</param>
    /// <param name="options">Optional event options.</param>
    public static void Error(string message, LSEventOptions? options = null);
    
    /// <summary>Sends a warning message.</summary>
    /// <param name="message">The warning message to send.</param>
    /// <param name="options">Optional event options.</param>
    public static void Warning(string message, LSEventOptions? options = null);
    
    /// <summary>Sends a print message.</summary>
    /// <param name="message">The message to print.</param>
    /// <param name="dispatcher">Optional dispatcher to use.</param>
    public static void Print(string message, LSDispatcher? dispatcher = null);
    
    /// <summary>Sends a notification.</summary>
    /// <param name="message">The notification message.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="allowDismiss">Whether the notification can be dismissed.</param>
    /// <param name="timeout">Timeout in seconds.</param>
    /// <param name="options">Optional event options.</param>
    public static void Notify(string message, string description = "", 
        bool allowDismiss = false, double timeout = 3f, LSEventOptions? options = null);
    
    /// <summary>Sends a confirmation dialog.</summary>
    /// <param name="title">The confirmation title.</param>
    /// <param name="description">The confirmation description.</param>
    /// <param name="buttonConfirmationLabel">The confirm button label.</param>
    /// <param name="buttonConfirmationCallback">The confirm button callback.</param>
    /// <param name="options">Optional event options.</param>
    public static void Confirmation(string title, string description, 
        string buttonConfirmationLabel, LSAction buttonConfirmationCallback, 
        LSEventOptions? options = null);
}
```

### LSTick

```csharp
namespace LSUtils;

/// <summary>Manages clock and notifies registered listeners when the clock ticks.</summary>
public class LSTick : ILSEventable {
    /// <summary>Gets the singleton instance of the tick manager.</summary>
    public static LSTick Singleton { get; }
    
    /// <summary>Gets the tick value in seconds.</summary>
    public readonly float TICK_VALUE;
    
    /// <summary>Gets the class name.</summary>
    public string ClassName { get; }
    
    /// <summary>Gets the unique identifier for this tick manager.</summary>
    public Guid ID { get; }
    
    /// <summary>Gets or sets the delta factor for time scaling.</summary>
    public int DeltaFactor { get; protected set; }
    
    /// <summary>Gets the dispatcher associated with this tick manager.</summary>
    public LSDispatcher? Dispatcher { get; protected set; }
    
    /// <summary>Initializes the tick manager with the specified options.</summary>
    /// <param name="options">The event options containing dispatcher and configuration.</param>
    public void Initialize(LSEventOptions options);
    
    /// <summary>Updates the tick count and notifies listeners.</summary>
    /// <param name="delta">The time since the last update.</param>
    public void Update(double delta);
    
    /// <summary>Updates the physics tick.</summary>
    /// <param name="delta">The time since the last physics update.</param>
    public void PhysicsUpdate(double delta);
    
    /// <summary>Starts the tick manager.</summary>
    public void Start();
    
    /// <summary>Toggles the pause state of the tick manager.</summary>
    public void TogglePause();
    
    /// <summary>Sets the delta factor for time scaling.</summary>
    /// <param name="value">The new delta factor value.</param>
    public void SetDeltaFactor(int value);
    
    /// <summary>Cleans up resources used by the tick manager.</summary>
    public void Cleanup();
}
```

### LSState<TState, TContext>

```csharp
namespace LSUtils;

/// <summary>Modern state implementation using the LSEventSystem.</summary>
/// <typeparam name="TState">The concrete state type.</typeparam>
/// <typeparam name="TContext">The context type.</typeparam>
public abstract class LSState<TState, TContext> : ILSState, ILSEventable
    where TState : LSState<TState, TContext>
    where TContext : ILSContext {
    
    /// <summary>Gets the dispatcher associated with this state.</summary>
    public LSDispatcher? Dispatcher { get; protected set; }
    
    /// <summary>Gets whether this state has been initialized.</summary>
    public bool IsInitialized { get; protected set; }
    
    /// <summary>Gets the unique identifier for this state instance.</summary>
    public virtual Guid ID { get; }
    
    /// <summary>Gets the context associated with this state.</summary>
    public TContext Context { get; protected set; }
    
    /// <summary>Gets the class name of this state instance.</summary>
    public virtual string ClassName { get; }
    
    /// <summary>Initializes a new instance of the state with the specified context.</summary>
    /// <param name="context">The context instance.</param>
    protected LSState(TContext context);
    
    /// <summary>Initializes this state with the specified options.</summary>
    /// <param name="options">The event options containing dispatcher and configuration.</param>
    public void Initialize(LSEventOptions options);
    
    /// <summary>Enters the state with optional callbacks.</summary>
    /// <typeparam name="T">The state type.</typeparam>
    /// <param name="enterCallback">Callback to execute when entering.</param>
    /// <param name="exitCallback">Callback to execute when exiting.</param>
    public void Enter<T>(LSAction<T> enterCallback, LSAction<T> exitCallback) where T : ILSState;
    
    /// <summary>Exits the state with an optional callback.</summary>
    /// <param name="callback">Callback to execute after exiting.</param>
    public void Exit(LSAction callback);
    
    /// <summary>Performs cleanup operations for the state.</summary>
    public abstract void Cleanup();
}
```

## Mathematical Utilities API

### LSMath

```csharp
namespace LSUtils;

/// <summary>Provides extended mathematical functions and utilities.</summary>
public static class LSMath {
    /// <summary>Clamps a value between a minimum and maximum value.</summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The clamped value.</returns>
    public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>;
    
    /// <summary>Linearly interpolates between two values.</summary>
    /// <param name="a">The start value.</param>
    /// <param name="b">The end value.</param>
    /// <param name="t">The interpolation factor (0-1).</param>
    /// <returns>The interpolated value.</returns>
    public static float Lerp(float a, float b, float t);
    
    /// <summary>Calculates the distance between two 2D points.</summary>
    /// <param name="x1">The x-coordinate of the first point.</param>
    /// <param name="y1">The y-coordinate of the first point.</param>
    /// <param name="x2">The x-coordinate of the second point.</param>
    /// <param name="y2">The y-coordinate of the second point.</param>
    /// <returns>The distance between the points.</returns>
    public static float Distance(float x1, float y1, float x2, float y2);
    
    /// <summary>Converts degrees to radians.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>The angle in radians.</returns>
    public static float DegToRad(float degrees);
    
    /// <summary>Converts radians to degrees.</summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The angle in degrees.</returns>
    public static float RadToDeg(float radians);
}
```

### ILSVector2

```csharp
namespace LSUtils;

/// <summary>Represents a 2D vector with floating-point coordinates.</summary>
public interface ILSVector2 {
    /// <summary>Gets or sets the x-coordinate.</summary>
    float X { get; set; }
    
    /// <summary>Gets or sets the y-coordinate.</summary>
    float Y { get; set; }
    
    /// <summary>Gets the magnitude (length) of the vector.</summary>
    float Magnitude { get; }
    
    /// <summary>Gets the squared magnitude of the vector.</summary>
    float SqrMagnitude { get; }
    
    /// <summary>Gets a normalized version of this vector.</summary>
    ILSVector2 Normalized { get; }
    
    /// <summary>Adds another vector to this vector.</summary>
    /// <param name="other">The vector to add.</param>
    /// <returns>The result of the addition.</returns>
    ILSVector2 Add(ILSVector2 other);
    
    /// <summary>Subtracts another vector from this vector.</summary>
    /// <param name="other">The vector to subtract.</param>
    /// <returns>The result of the subtraction.</returns>
    ILSVector2 Subtract(ILSVector2 other);
    
    /// <summary>Multiplies this vector by a scalar value.</summary>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>The result of the multiplication.</returns>
    ILSVector2 Multiply(float scalar);
    
    /// <summary>Calculates the dot product with another vector.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The dot product.</returns>
    float Dot(ILSVector2 other);
    
    /// <summary>Calculates the distance to another vector.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The distance between the vectors.</returns>
    float DistanceTo(ILSVector2 other);
}
```

## Collections API

### BinaryHeap<T>

```csharp
namespace LSUtils.Collections;

/// <summary>Efficient binary heap implementation for priority queue operations.</summary>
/// <typeparam name="T">The type of elements in the heap.</typeparam>
public class BinaryHeap<T> where T : IComparable<T> {
    /// <summary>Gets the number of elements in the heap.</summary>
    public int Count { get; }
    
    /// <summary>Gets whether the heap is empty.</summary>
    public bool IsEmpty { get; }
    
    /// <summary>Initializes a new instance of the BinaryHeap class.</summary>
    public BinaryHeap();
    
    /// <summary>Initializes a new instance with the specified capacity.</summary>
    /// <param name="capacity">The initial capacity of the heap.</param>
    public BinaryHeap(int capacity);
    
    /// <summary>Adds an element to the heap.</summary>
    /// <param name="item">The element to add.</param>
    public void Add(T item);
    
    /// <summary>Removes and returns the minimum element from the heap.</summary>
    /// <returns>The minimum element.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the heap is empty.</exception>
    public T RemoveMin();
    
    /// <summary>Returns the minimum element without removing it.</summary>
    /// <returns>The minimum element.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the heap is empty.</exception>
    public T PeekMin();
    
    /// <summary>Removes all elements from the heap.</summary>
    public void Clear();
    
    /// <summary>Determines whether the heap contains a specific element.</summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>true if the element is found; otherwise, false.</returns>
    public bool Contains(T item);
}
```

### CachePool<T>

```csharp
namespace LSUtils.Collections;

/// <summary>Object pool implementation with cache management.</summary>
/// <typeparam name="T">The type of objects to pool.</typeparam>
public class CachePool<T> : ICachePool<T> where T : class, new() {
    /// <summary>Gets the maximum capacity of the pool.</summary>
    public int MaxCapacity { get; }
    
    /// <summary>Gets the current number of objects in the pool.</summary>
    public int Count { get; }
    
    /// <summary>Gets whether the pool is empty.</summary>
    public bool IsEmpty { get; }
    
    /// <summary>Gets whether the pool is at maximum capacity.</summary>
    public bool IsFull { get; }
    
    /// <summary>Initializes a new instance with default capacity.</summary>
    public CachePool();
    
    /// <summary>Initializes a new instance with the specified capacity.</summary>
    /// <param name="maxCapacity">The maximum number of objects to pool.</param>
    public CachePool(int maxCapacity);
    
    /// <summary>Gets an object from the pool or creates a new one.</summary>
    /// <returns>An object instance.</returns>
    public T Get();
    
    /// <summary>Returns an object to the pool.</summary>
    /// <param name="item">The object to return.</param>
    /// <returns>true if the object was returned to the pool; false if the pool is full.</returns>
    public bool Return(T item);
    
    /// <summary>Clears all objects from the pool.</summary>
    public void Clear();
    
    /// <summary>Preloads the pool with the specified number of objects.</summary>
    /// <param name="count">The number of objects to preload.</param>
    public void Preload(int count);
}
```

## Graph Algorithms API

### GridGraph

```csharp
namespace LSUtils.Graphs;

/// <summary>Represents a grid-based graph for pathfinding algorithms.</summary>
public class GridGraph : IGraph<GridNode> {
    /// <summary>Gets the width of the grid.</summary>
    public int Width { get; }
    
    /// <summary>Gets the height of the grid.</summary>
    public int Height { get; }
    
    /// <summary>Initializes a new grid graph with the specified dimensions.</summary>
    /// <param name="width">The width of the grid.</param>
    /// <param name="height">The height of the grid.</param>
    public GridGraph(int width, int height);
    
    /// <summary>Gets the node at the specified coordinates.</summary>
    /// <param name="x">The x-coordinate.</param>
    /// <param name="y">The y-coordinate.</param>
    /// <returns>The node at the specified position, or null if out of bounds.</returns>
    public GridNode? GetNode(int x, int y);
    
    /// <summary>Sets whether a node is walkable.</summary>
    /// <param name="x">The x-coordinate.</param>
    /// <param name="y">The y-coordinate.</param>
    /// <param name="walkable">Whether the node should be walkable.</param>
    public void SetWalkable(int x, int y, bool walkable);
    
    /// <summary>Gets the neighbors of the specified node.</summary>
    /// <param name="node">The node to get neighbors for.</param>
    /// <returns>A collection of neighboring nodes.</returns>
    public IEnumerable<GridNode> GetNeighbors(GridNode node);
    
    /// <summary>Calculates the cost of moving from one node to another.</summary>
    /// <param name="from">The starting node.</param>
    /// <param name="to">The destination node.</param>
    /// <returns>The movement cost.</returns>
    public float GetMovementCost(GridNode from, GridNode to);
    
    /// <summary>Calculates the heuristic distance between two nodes.</summary>
    /// <param name="from">The starting node.</param>
    /// <param name="to">The destination node.</param>
    /// <returns>The heuristic distance.</returns>
    public float GetHeuristicDistance(GridNode from, GridNode to);
}
```

### AStarPathResolver<T>

```csharp
namespace LSUtils.Graphs;

/// <summary>A* pathfinding algorithm implementation.</summary>
/// <typeparam name="T">The type of nodes in the graph.</typeparam>
public class AStarPathResolver<T> : IPathResolver<T> where T : IGraphNode {
    /// <summary>Initializes a new instance of the A* path resolver.</summary>
    public AStarPathResolver();
    
    /// <summary>Finds the shortest path between two nodes using the A* algorithm.</summary>
    /// <param name="start">The starting node.</param>
    /// <param name="goal">The goal node.</param>
    /// <param name="graph">The graph to search in.</param>
    /// <returns>A list of nodes representing the path, or null if no path exists.</returns>
    public List<T>? FindPath(T start, T goal, IGraph<T> graph);
    
    /// <summary>Finds the shortest path with additional options.</summary>
    /// <param name="start">The starting node.</param>
    /// <param name="goal">The goal node.</param>
    /// <param name="graph">The graph to search in.</param>
    /// <param name="maxIterations">Maximum number of iterations to prevent infinite loops.</param>
    /// <returns>A list of nodes representing the path, or null if no path exists.</returns>
    public List<T>? FindPath(T start, T goal, IGraph<T> graph, int maxIterations);
}
```

## Examples and Usage Patterns

### Basic Event Processing

```csharp
// Define a custom event
public class OrderProcessingEvent : LSEvent {
    public int OrderId { get; }
    public decimal Amount { get; }
    
    public OrderProcessingEvent(LSEventOptions options, int orderId, decimal amount) 
        : base(options) {
        OrderId = orderId;
        Amount = amount;
        SetData("order_id", orderId);
        SetData("amount", amount);
    }
}

// Register global handlers
var dispatcher = LSDispatcher.Singleton;

dispatcher.ForEventPhase<OrderProcessingEvent, BusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSPriority.HIGH)
        .Handler(ctx => {
            var amount = ctx.Event.GetData<decimal>("amount");
            if (amount <= 0) {
                ctx.Event.SetData("error", "Invalid amount");
                return HandlerProcessResult.FAILURE;
            }
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Process an order
var options = new LSEventOptions(dispatcher);
var orderEvent = new OrderProcessingEvent(options, 12345, 99.99m);
var result = orderEvent.Dispatch();
```

### Asynchronous Processing

```csharp
// Handler that waits for external input
dispatcher.ForEventPhase<OrderProcessingEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            var orderId = ctx.Event.GetData<int>("order_id");
            
            // Start async payment processing
            var paymentId = StartPaymentProcessing(orderId);
            ctx.Event.SetData("payment_id", paymentId);
            
            // Register callback for when payment completes
            RegisterPaymentCallback(paymentId, (success) => {
                var context = GetEventContext(ctx.Event.ID);
                if (success) {
                    context.Resume(); // Continue processing
                } else {
                    context.Fail(); // Mark as failed
                }
            });
            
            return HandlerProcessResult.WAITING; // Pause processing
        })
        .Build());
```

### Event-Scoped Handlers

```csharp
// Create event with custom logic
var options = new LSEventOptions(dispatcher);
var orderEvent = new OrderProcessingEvent(options, 12345, 99.99m)
    .WithPhaseCallbacks<BusinessState.ValidatePhaseState>(
        register => register
            .Handler(ctx => {
                // Custom validation logic for this specific order
                var orderId = ctx.Event.GetData<int>("order_id");
                if (IsHighRiskOrder(orderId)) {
                    return HandlerProcessResult.CANCELLED;
                }
                return HandlerProcessResult.SUCCESS;
            })
            .Build()
    )
    .OnSuccess(evt => {
        // Success callback
        Console.WriteLine($"Order {evt.GetData<int>("order_id")} processed successfully!");
    });

var result = orderEvent.Dispatch();
```

### Pathfinding Example

```csharp
// Create a grid graph
var graph = new GridGraph(20, 20);

// Set some obstacles
graph.SetWalkable(5, 5, false);
graph.SetWalkable(6, 5, false);
graph.SetWalkable(7, 5, false);

// Find path using A*
var pathfinder = new AStarPathResolver<GridNode>();
var path = pathfinder.FindPath(
    graph.GetNode(0, 0),   // Start position
    graph.GetNode(19, 19), // Goal position
    graph
);

if (path != null) {
    Console.WriteLine($"Path found with {path.Count} steps:");
    foreach (var node in path) {
        Console.WriteLine($"  -> ({node.X}, {node.Y})");
    }
} else {
    Console.WriteLine("No path found!");
}
```

### Tick System Usage

```csharp
// Initialize tick system
var tick = LSTick.Singleton;
var options = new LSEventOptions();
tick.Initialize(options);

// Subscribe to tick events
options.Dispatcher.ForEvent<LSTick.OnTickEvent>(register => register
    .OnPhase<BusinessState.ExecutePhaseState>(phase => phase
        .Handler(ctx => {
            var tickCount = ctx.Event.Instance.DeltaFactor;
            // Update game logic here
            UpdateGameLogic(tickCount);
            return HandlerProcessResult.SUCCESS;
        })
        .Build())
    .Register());

// In your main loop
while (gameRunning) {
    var deltaTime = CalculateDeltaTime();
    tick.Update(deltaTime);
    
    // Other game loop logic...
}
```

This API documentation provides comprehensive coverage of the LSUtils library components with detailed method signatures, parameter descriptions, and practical usage examples. The documentation follows standard C# XML documentation conventions and includes both basic and advanced usage patterns.
