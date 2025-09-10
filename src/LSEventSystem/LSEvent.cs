using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// V4 base event class with clean state machine and simplified design.
/// 
/// This abstract class serves as the foundation for all events in the LSEventSystem v4,
/// providing a clean, state-machine-based approach to event processing. The system
/// processes events through sequential phases: VALIDATE → CONFIGURE → EXECUTE → CLEANUP.
/// 
/// Key Features:
/// - State machine-based processing with well-defined phase transitions
/// - Thread-safe data storage with concurrent dictionary
/// - Integration with LSESDispatcher for handler registration and execution
/// - Support for both global handlers (via dispatcher) and event-scoped handlers
/// - Failure and cancellation handling with proper state management
/// - Asynchronous operation support through waiting states
/// 
/// Event Lifecycle:
/// 1. Event creation with dispatcher injection
/// 2. Handler registration (global or event-scoped)
/// 3. Dispatch() - begins state machine processing
/// 4. Sequential phase execution: Business → Success/Failure → Completed
/// 5. Terminal states: Completed or Cancelled
/// 
/// Usage Example:
/// <code>
/// public class UserRegistrationEvent : BaseEvent {
///     public string Email { get; }
///     public string Name { get; }
///     
///     public UserRegistrationEvent(LSESDispatcher dispatcher, string email, string name) 
///         : base(dispatcher) {
///         Email = email;
///         Name = name;
///     }
/// }
/// 
/// // Usage with event-scoped handlers
/// var evt = new UserRegistrationEvent(dispatcher, "user@example.com", "John Doe");
/// var result = evt.WithPhaseCallbacks&lt;BusinessState.ValidatePhaseState&gt;(
///     register => register
///         .Handler(ctx => {
///             // Validation logic
///             return HandlerProcessResult.SUCCESS;
///         })
///         .Build()
/// ).Dispatch();
/// </code>
/// 
/// Thread Safety:
/// - Data storage operations are thread-safe via ConcurrentDictionary
/// - State transitions are handled by the state machine implementation
/// - Handler execution follows the phase-based sequential model
/// 
/// See also: <see cref="ILSEvent"/>, <see cref="LSDispatcher"/>, <see cref="LSEventProcessContext"/>
/// </summary>
public abstract class LSEvent : ILSEvent {
    private readonly ConcurrentDictionary<string, object> _data = new();
    private readonly LSDispatcher _dispatcher;
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
    //public EventSystemPhase CurrentPhase { get; internal set; } = EventSystemPhase.VALIDATE;

    /// <summary>
    /// Phases that have been completed successfully.
    /// </summary>
    //public EventSystemPhase CompletedPhases { get; internal set; }

    /// <summary>
    /// Indicates if the event processing was cancelled.
    /// </summary>
    public bool IsCancelled => _isCanceled();
    protected Func<bool> _isCanceled = () => false;

    /// <summary>
    /// Indicates if the event has failures but processing can continue.
    /// In v4, this is determined by phase results rather than a separate flag.
    /// </summary>
    public bool HasFailures => _hasFailures();
    protected Func<bool> _hasFailures = () => false;

    /// <summary>
    /// Indicates if the event has completed processing.
    /// </summary>
    public bool IsCompleted { get; internal set; }
    public bool InDispatch { get; internal set; }
    /// <summary>
    /// Read-only access to event data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data => _data;

    private LSEvent() {
        throw new LSException("Default constructor is not allowed. Use the constructor with dispatcher parameter.");
    }
    //constructor with dispatcher
    protected LSEvent(LSDispatcher dispatcher) {
        _dispatcher = dispatcher ?? throw new LSArgumentNullException(nameof(dispatcher));
    }
    /// <summary>
    /// Gets or sets data associated with this event instance.
    /// Thread-safe operation using internal concurrent dictionary.
    /// </summary>
    /// <typeparam name="T">The type of value to store</typeparam>
    /// <param name="key">The unique key for the data</param>
    /// <param name="value">The value to store</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
    /// <example>
    /// <code>
    /// event.SetData("user_id", 12345);
    /// event.SetData("validation_errors", new List&lt;string&gt;());
    /// event.SetData("processing_timestamp", DateTime.UtcNow);
    /// </code>
    /// </example>
    public virtual void SetData<T>(string key, T value) {
        _data[key] = (object)value!;
    }

    /// <summary>
    /// Retrieves data associated with this event instance.
    /// Thread-safe operation using internal concurrent dictionary.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored value</typeparam>
    /// <param name="key">The unique key for the data</param>
    /// <returns>The stored value if found and of correct type, otherwise default(T)</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
    /// <example>
    /// <code>
    /// var userId = event.GetData&lt;int&gt;("user_id");
    /// var errors = event.GetData&lt;List&lt;string&gt;&gt;("validation_errors") ?? new List&lt;string&gt;();
    /// </code>
    /// </example>
    public virtual T GetData<T>(string key) {
        if (_data.TryGetValue(key, out var value) && value is T typedValue) {
            return typedValue;
        }
        return default(T)!;
    }

    /// <summary>
    /// Attempts to retrieve data associated with this event instance.
    /// Thread-safe operation using internal concurrent dictionary.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored value</typeparam>
    /// <param name="key">The unique key for the data</param>
    /// <param name="value">When this method returns, contains the stored value if found and of correct type</param>
    /// <returns>true if the key was found and the value is of the correct type; otherwise, false</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
    /// <example>
    /// <code>
    /// if (event.TryGetData("user_id", out int userId)) {
    ///     // Use userId safely
    ///     ProcessUser(userId);
    /// }
    /// </code>
    /// </example>
    public virtual bool TryGetData<T>(string key, out T value) {
        if (_data.TryGetValue(key, out var obj) && obj is T typedValue) {
            value = typedValue;
            return true;
        }
        value = default(T)!;
        return false;
    }

    /// <summary>
    /// Dispatches this event through the configured dispatcher for processing.
    /// 
    /// Initiates the state machine-based event processing pipeline:
    /// 1. Retrieves registered handlers from the dispatcher
    /// 2. Adds any event-scoped handlers configured via WithPhaseCallbacks()
    /// 3. Creates an EventSystemContext for state management
    /// 4. Processes the event through the state machine until completion
    /// 
    /// Processing Flow:
    /// - Business State: VALIDATE → CONFIGURE → EXECUTE → CLEANUP
    /// - Success/Failure transitions based on handler results
    /// - Terminal states: Completed or Cancelled
    /// 
    /// Thread Safety: This method is not thread-safe and should only be called once per event instance.
    /// </summary>
    /// <returns>
    /// The final processing result:
    /// - SUCCESS: All phases completed successfully
    /// - FAILURE: Event failed but completed processing
    /// - CANCELLED: Event was cancelled during processing
    /// - WAITING: Event is waiting for external input (should not occur from this method)
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when dispatcher is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when event has already been dispatched</exception>
    /// <example>
    /// <code>
    /// var event = new UserRegistrationEvent(dispatcher, email, name);
    /// var result = event.Dispatch();
    /// 
    /// switch (result) {
    ///     case EventProcessResult.SUCCESS:
    ///         Console.WriteLine("User registered successfully");
    ///         break;
    ///     case EventProcessResult.FAILURE:
    ///         Console.WriteLine("Registration failed but was handled gracefully");
    ///         break;
    ///     case EventProcessResult.CANCELLED:
    ///         Console.WriteLine("Registration was cancelled");
    ///         break;
    /// }
    /// </code>
    /// </example>
    public EventProcessResult Dispatch() {
        if (_dispatcher == null) throw new ArgumentNullException(nameof(_dispatcher));
        List<IHandlerEntry> handlers = _dispatcher.getHandlers(GetType());
        if (_eventHandlers.Count > 0) handlers.AddRange(_eventHandlers);

        InDispatch = true;
        var context = LSEventProcessContext.Create(_dispatcher, this, handlers);
        _isCanceled = () => context.IsCancelled;
        _hasFailures = () => context.HasFailures;
        return context.processEvent();
    }
    /// <summary>
    /// List of event-scoped handlers that will be added to the processing pipeline.
    /// These handlers are specific to this event instance and are not shared globally.
    /// </summary>
    List<IHandlerEntry> _eventHandlers = new();
    
    /// <summary>
    /// Configures event-scoped phase handlers for this event instance.
    /// 
    /// Event-scoped handlers are executed alongside global handlers but are specific to this event.
    /// They allow for dynamic behavior configuration without affecting the global dispatcher state.
    /// 
    /// Handler Priority and Execution Order:
    /// - Handlers within the same phase execute in priority order (CRITICAL → HIGH → NORMAL → LOW → BACKGROUND)
    /// - Event-scoped handlers are mixed with global handlers based on their priority
    /// - Multiple configurations can be chained together
    /// </summary>
    /// <typeparam name="TPhase">The specific phase state type (ValidatePhaseState, ConfigurePhaseState, etc.)</typeparam>
    /// <param name="configure">Array of configuration functions that create phase handler entries</param>
    /// <returns>This event instance for method chaining</returns>
    /// <exception cref="LSArgumentNullException">Thrown when any configuration function returns null</exception>
    /// <example>
    /// <code>
    /// var event = new UserRegistrationEvent(dispatcher, email, name)
    ///     .WithPhaseCallbacks&lt;BusinessState.ValidatePhaseState&gt;(
    ///         register => register
    ///             .WithPriority(LSESPriority.HIGH)
    ///             .Handler(ctx => {
    ///                 if (string.IsNullOrEmpty(ctx.Event.GetData&lt;string&gt;("email"))) {
    ///                     return HandlerProcessResult.FAILURE;
    ///                 }
    ///                 return HandlerProcessResult.SUCCESS;
    ///             })
    ///             .Build()
    ///     )
    ///     .WithPhaseCallbacks&lt;BusinessState.ExecutePhaseState&gt;(
    ///         register => register
    ///             .Handler(ctx => {
    ///                 // Execute registration logic
    ///                 return HandlerProcessResult.SUCCESS;
    ///             })
    ///             .Build()
    ///     );
    /// 
    /// var result = event.Dispatch();
    /// </code>
    /// </example>
    public ILSEvent WithPhaseCallbacks<TPhase>(params Func<LSPhaseHandlerRegister<TPhase>, LSPhaseHandlerEntry>[] configure) where TPhase : BusinessState.PhaseState {
        foreach (var config in configure) {
            var register = LSPhaseHandlerRegister<TPhase>.Create(_dispatcher);
            var entry = config(register);
            if (entry == null) throw new LSArgumentNullException(nameof(entry));
            _eventHandlers.Add(entry);
        }
        return this;
    }

}
