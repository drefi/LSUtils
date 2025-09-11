namespace LSUtils.EventSystem;

/// <summary>
/// Factory class for creating initialization events for ILSEventable objects.
/// 
/// Provides both static factory methods and type-safe event creation for objects that
/// implement the ILSEventable interface. This is commonly used during object initialization
/// to trigger event-based setup and configuration logic.
/// 
/// Usage Patterns:
/// - Use Create() for simple factory-based event creation
/// - Use constructor directly for advanced scenarios with custom options
/// - Common in LSState, LSTick, and other eventable component initialization
/// 
/// Example:
/// <code>
/// var options = new LSEventOptions(dispatcher, ownerInstance);
/// var initEvent = OnInitializeEvent.Create(myComponent, options);
/// initEvent.Dispatch();
/// </code>
/// </summary>
public static class OnInitializeEvent {
    /// <summary>
    /// Creates a new initialization event for the specified eventable instance.
    /// 
    /// This factory method provides a convenient way to create initialization events
    /// without needing to specify the generic type parameter explicitly. The type
    /// is inferred from the instance parameter.
    /// </summary>
    /// <typeparam name="TInstance">The type of the eventable instance being initialized</typeparam>
    /// <param name="instance">The eventable instance to initialize</param>
    /// <param name="options">Event options containing dispatcher and configuration</param>
    /// <returns>A new OnInitializeEvent for the specified instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when instance or options is null</exception>
    public static OnInitializeEvent<TInstance> Create<TInstance>(TInstance instance, LSEventOptions options) where TInstance : ILSEventable {
        var @event = new OnInitializeEvent<TInstance>(instance, options);
        return @event;
    }
}

/// <summary>
/// Event triggered when an ILSEventable object is being initialized.
/// 
/// This event is dispatched during the initialization phase of eventable objects
/// to allow handlers to perform setup logic, validation, and configuration tasks.
/// The event carries a reference to the instance being initialized for context.
/// 
/// Common use cases:
/// - Setting up initial state and configuration
/// - Validating initialization parameters
/// - Registering the object with external systems
/// - Establishing event handler subscriptions
/// - Performing resource allocation
/// 
/// Handler Registration:
/// Handlers can be registered globally via the dispatcher or attached as event-scoped
/// handlers during initialization. The event follows the standard v4 phase model.
/// 
/// Example Usage:
/// <code>
/// // Global handler registration
/// dispatcher.ForEventPhase&lt;OnInitializeEvent&lt;MyComponent&gt;, BusinessState.ValidatePhaseState&gt;(
///     register => register
///         .Handler(ctx => {
///             var component = ctx.Event.Instance;
///             // Validate component state
///             return HandlerProcessResult.SUCCESS;
///         })
///         .Build());
/// 
/// // Event creation and dispatch
/// var options = new LSEventOptions(dispatcher, component);
/// var initEvent = new OnInitializeEvent&lt;MyComponent&gt;(component, options);
/// var result = initEvent.Dispatch();
/// </code>
/// </summary>
/// <typeparam name="TInstance">The type of eventable instance being initialized</typeparam>
public class OnInitializeEvent<TInstance> : LSEvent<TInstance> where TInstance : ILSEventable {
    /// <summary>
    /// Initializes a new OnInitializeEvent for the specified eventable instance.
    /// 
    /// The event will be associated with the provided instance and configured with
    /// the specified options. The instance will be available through the Instance
    /// property for handlers to access during processing.
    /// </summary>
    /// <param name="instance">The eventable instance being initialized</param>
    /// <param name="options">Event options containing dispatcher and configuration</param>
    /// <exception cref="ArgumentNullException">Thrown when instance is null</exception>
    public OnInitializeEvent(TInstance instance, LSEventOptions? options) : base(instance, options) {
    }

    /// <summary>
    /// Creates a new initialization event for the specified eventable instance.
    /// 
    /// This static factory method provides an alternative to the constructor
    /// and can be used when a more explicit creation pattern is preferred.
    /// Functionally equivalent to using the constructor directly.
    /// </summary>
    /// <param name="instance">The eventable instance to initialize</param>
    /// <param name="options">Event options containing dispatcher and configuration</param>
    /// <returns>A new OnInitializeEvent for the specified instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when instance or options is null</exception>
    public static OnInitializeEvent<TInstance> Create(TInstance instance, LSEventOptions options) {
        var @event = new OnInitializeEvent<TInstance>(instance, options);
        return @event;
    }
}
