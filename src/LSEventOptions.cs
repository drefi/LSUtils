namespace LSUtils;

/// <summary>
/// Base configuration options for LSEvent instances, including callbacks, timing, and dispatcher settings.
/// </summary>
/// <remarks>
/// This class provides comprehensive configuration for events, including success/failure/cancellation
/// callbacks, timeout settings, custom dispatchers, and error handling. It serves as the foundation
/// for all event configuration in the LSUtils system.
/// 
/// <para>
/// The class supports multiple initialization patterns:
/// - Static Create methods for basic setup
/// - Create method overloads for common scenarios
/// - Fluent API for readable method chaining
/// - Static factory methods for specific use cases
/// - Copy constructor for inheriting dispatcher and error handler
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Basic usage
/// var options = LSEventOptions.Create();
/// 
/// // Create with callbacks
/// var options = LSEventOptions.Create(onSuccess: () => Console.WriteLine("Success"));
/// 
/// // Fluent API
/// var options = LSEventOptions.Create()
///     .WithDispatcher(customDispatcher)
///     .WithSuccess(() => Console.WriteLine("Done"))
///     .WithTimeout(5.0f);
/// 
/// // Factory method for event chaining
/// var childOptions = LSEventOptions.ForEvent(parentEvent);
/// 
/// // Copy dispatcher and error handler from existing options
/// var newOptions = LSEventOptions.Create(existingOptions)
///     .WithSuccess(() => Console.WriteLine("New success callback"));
/// </code>
/// </example>
public class LSEventOptions {
    #region Private Fields
    /// <summary>
    /// Internal event handlers for the various event lifecycle callbacks.
    /// </summary>
    public event LSMessageHandler? ErrorHandler;
    public event LSAction? OnDispatch;
    public event LSAction? OnSuccess;
    public event LSAction<string>? OnFailure;
    public event LSAction? OnCancel;
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventOptions"/> class with default settings.
    /// </summary>
    /// <remarks>
    /// Creates event options with a new unique ID, no timeout, the default dispatcher,
    /// and static listener grouping.
    /// </remarks>
    protected LSEventOptions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventOptions"/> class with specific dispatcher and callbacks.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for event handling. If null, uses the default instance.</param>
    /// <param name="onSuccess">Optional callback to invoke when the event succeeds.</param>
    /// <param name="onFailure">Optional callback to invoke when the event fails.</param>
    /// <param name="errorHandler">Optional error handler for processing error messages.</param>
    /// <remarks>
    /// This constructor provides a convenient way to set up event options with the most commonly used parameters.
    /// All parameters except the dispatcher can be null if not needed.
    /// </remarks>
    protected LSEventOptions(LSDispatcher? dispatcher, LSAction? onSuccess, LSAction<string>? onFailure, LSMessageHandler? errorHandler) {
        Dispatcher = dispatcher ?? LSDispatcher.Instance;
        OnSuccess = onSuccess;
        OnFailure = onFailure;
        ErrorHandler = errorHandler;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventOptions"/> class with success and failure callbacks.
    /// </summary>
    /// <param name="onSuccess">Optional callback to invoke when the event succeeds.</param>
    /// <param name="onFailure">Optional callback to invoke when the event fails.</param>
    /// <remarks>
    /// This constructor is useful when you only need to set up basic success and failure handling
    /// without customizing the dispatcher or error handling.
    /// </remarks>
    protected LSEventOptions(LSAction? onSuccess, LSAction<string>? onFailure = null) {
        OnSuccess = onSuccess;
        OnFailure = onFailure;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventOptions"/> class with a specific dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for event handling. If null, uses the default instance.</param>
    /// <param name="onSuccess">Optional callback to invoke when the event succeeds.</param>
    /// <remarks>
    /// This constructor is useful when you need to specify a custom dispatcher but don't need
    /// complex callback setup during construction.
    /// </remarks>
    protected LSEventOptions(LSDispatcher dispatcher, LSAction? onSuccess = null) {
        Dispatcher = dispatcher;
        OnSuccess = onSuccess;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventOptions"/> class by copying dispatcher and error handler from another instance.
    /// </summary>
    /// <param name="copyFrom">The source options to copy dispatcher and error handler from.</param>
    /// <remarks>
    /// Creates a new options instance copying only the dispatcher and error handler from the source.
    /// The new instance gets a new unique ID and default values for all other settings.
    /// This is useful for creating new options that use the same infrastructure settings.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="copyFrom"/> is null.</exception>
    protected LSEventOptions(LSEventOptions copyFrom) {
        if (copyFrom == null) throw new System.ArgumentNullException(nameof(copyFrom));

        ID = System.Guid.NewGuid(); // New ID for the copy
        Dispatcher = copyFrom.Dispatcher;
        if (copyFrom.ErrorHandler != null) ErrorHandler += copyFrom.ErrorHandler;
    }

    #endregion

    #region Static Create Methods
    /// <summary>
    /// Creates a new instance of the <see cref="LSEventOptions"/> class with default settings.
    /// </summary>
    /// <returns>A new LSEventOptions instance with default settings.</returns>
    /// <remarks>
    /// Creates event options with a new unique ID, no timeout, the default dispatcher,
    /// and static listener grouping.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create();
    /// </code>
    /// </example>
    public static LSEventOptions Create() {
        return new LSEventOptions();
    }

    /// <summary>
    /// Creates a new instance of the <see cref="LSEventOptions"/> class with specific dispatcher and callbacks.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for event handling. If null, uses the default instance.</param>
    /// <param name="onSuccess">Optional callback to invoke when the event succeeds.</param>
    /// <param name="onFailure">Optional callback to invoke when the event fails.</param>
    /// <param name="errorHandler">Optional error handler for processing error messages.</param>
    /// <returns>A new LSEventOptions instance with the specified settings.</returns>
    /// <remarks>
    /// This method provides a convenient way to create event options with the most commonly used parameters.
    /// All parameters except the dispatcher can be null if not needed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create(
    ///     dispatcher: customDispatcher,
    ///     onSuccess: () => Console.WriteLine("Operation completed"),
    ///     onFailure: (msg) => Console.WriteLine($"Failed: {msg}"),
    ///     errorHandler: (msg) => { Console.Error.WriteLine(msg); return true; }
    /// );
    /// </code>
    /// </example>
    public static LSEventOptions Create(LSDispatcher? dispatcher, LSAction? onSuccess = null, LSAction<string>? onFailure = null, LSMessageHandler? errorHandler = null) {
        return new LSEventOptions(dispatcher, onSuccess, onFailure, errorHandler);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="LSEventOptions"/> class with success and failure callbacks.
    /// </summary>
    /// <param name="onSuccess">Optional callback to invoke when the event succeeds.</param>
    /// <param name="onFailure">Optional callback to invoke when the event fails.</param>
    /// <returns>A new LSEventOptions instance with the specified callbacks.</returns>
    /// <remarks>
    /// This method is useful when you only need to set up basic success and failure handling
    /// without customizing the dispatcher or error handling.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create(
    ///     onSuccess: () => Console.WriteLine("Done!"),
    ///     onFailure: (msg) => Console.WriteLine($"Error: {msg}")
    /// );
    /// </code>
    /// </example>
    public static LSEventOptions Create(LSAction? onSuccess, LSAction<string>? onFailure = null) {
        return new LSEventOptions(onSuccess, onFailure);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="LSEventOptions"/> class with a specific dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for event handling. If null, uses the default instance.</param>
    /// <param name="onSuccess">Optional callback to invoke when the event succeeds.</param>
    /// <returns>A new LSEventOptions instance with the specified dispatcher.</returns>
    /// <remarks>
    /// This method is useful when you need to specify a custom dispatcher but don't need
    /// complex callback setup during construction.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create(customDispatcher, () => Console.WriteLine("Success"));
    /// </code>
    /// </example>
    public static LSEventOptions Create(LSDispatcher dispatcher, LSAction? onSuccess = null) {
        return new LSEventOptions(dispatcher, onSuccess);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="LSEventOptions"/> class by copying dispatcher and error handler from another instance.
    /// </summary>
    /// <param name="copyFrom">The source options to copy dispatcher and error handler from.</param>
    /// <returns>A new LSEventOptions instance with copied dispatcher and error handler.</returns>
    /// <remarks>
    /// Creates a new options instance copying only the dispatcher and error handler from the source.
    /// The new instance gets a new unique ID and default values for all other settings.
    /// This is useful for creating new options that use the same infrastructure settings.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="copyFrom"/> is null.</exception>
    /// <example>
    /// <code>
    /// var baseOptions = LSEventOptions.Create()
    ///     .WithTimeout(10.0f)
    ///     .WithDispatcher(customDispatcher);
    /// 
    /// var derivedOptions = LSEventOptions.Create(baseOptions)
    ///     .WithSuccess(() => Console.WriteLine("Derived success"));
    /// </code>
    /// </example>
    public static LSEventOptions Create(LSEventOptions copyFrom) {
        return new LSEventOptions(copyFrom);
    }
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets or sets the unique identifier for the event.
    /// </summary>
    /// <value>A GUID that uniquely identifies the event. Defaults to a new GUID.</value>
    public System.Guid ID { get; set; } = System.Guid.NewGuid();

    /// <summary>
    /// Gets or sets the timeout duration for the event in seconds.
    /// </summary>
    /// <value>
    /// The timeout in seconds. A value of 0 or less means no timeout is applied.
    /// Default is 0 (no timeout).
    /// </value>
    /// <remarks>
    /// When a timeout is set, the event will automatically fail with a timeout message
    /// if it doesn't complete within the specified duration.
    /// </remarks>
    public float Timeout { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the dispatcher to use for this event.
    /// </summary>
    /// <value>
    /// The LSDispatcher instance to handle event dispatching. Defaults to the singleton instance.
    /// </value>
    /// <remarks>
    /// The dispatcher is responsible for routing events to registered listeners and managing
    /// the event lifecycle. Most events can use the default dispatcher instance.
    /// </remarks>
    public LSDispatcher Dispatcher { get; set; } = LSDispatcher.Instance;

    /// <summary>
    /// Gets or sets the listener group type that determines how listeners are matched.
    /// </summary>
    /// <value>
    /// The grouping strategy for listener matching. Defaults to <see cref="ListenerGroupType.STATIC"/>.
    /// </value>
    /// <remarks>
    /// The group type determines how the dispatcher matches this event with registered listeners.
    /// Static grouping matches by event type only, while subset grouping also considers instances.
    /// </remarks>
    public virtual ListenerGroupType GroupType { get; set; } = ListenerGroupType.STATIC;
    #endregion

    #region Fluent API Methods
    /// <summary>
    /// Sets the dispatcher for this event options instance using fluent syntax.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use. If null, uses the default instance.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method supports fluent configuration by returning the current instance,
    /// allowing multiple configuration calls to be chained together.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithDispatcher(customDispatcher)
    ///     .WithTimeout(5.0f);
    /// </code>
    /// </example>
    public LSEventOptions WithDispatcher(LSDispatcher dispatcher) {
        Dispatcher = dispatcher;
        return this;
    }

    /// <summary>
    /// Adds a dispatch callback to this event options instance using fluent syntax.
    /// </summary>
    /// <param name="onDispatch">The callback to invoke when the event is dispatched.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method adds the callback to the existing dispatch handlers rather than replacing them.
    /// Dispatch callbacks are invoked when the event is initially dispatched to listeners.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithDispatch(() => Console.WriteLine("Event dispatched"));
    /// </code>
    /// </example>
    public LSEventOptions WithDispatch(LSAction onDispatch) {
        OnDispatch += onDispatch;
        return this;
    }

    /// <summary>
    /// Sets the dispatch callback for this event options instance using fluent syntax, replacing any existing handlers.
    /// </summary>
    /// <param name="onDispatch">The callback to invoke when the event is dispatched.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method replaces all existing dispatch handlers with the new callback.
    /// Dispatch callbacks are invoked when the event is initially dispatched to listeners.
    /// Use this when you want to ensure only one dispatch handler is registered.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .SetDispatch(() => Console.WriteLine("Event dispatched"));
    /// </code>
    /// </example>
    public LSEventOptions SetDispatch(LSAction onDispatch) {
        OnDispatch = onDispatch;
        return this;
    }

    /// <summary>
    /// Sets the timeout for this event options instance using fluent syntax.
    /// </summary>
    /// <param name="timeout">The timeout duration in seconds. Values ≤ 0 disable timeout.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method supports fluent configuration by returning the current instance.
    /// Setting a timeout will cause the event to automatically fail if it doesn't complete
    /// within the specified duration.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithTimeout(10.0f)
    ///     .WithSuccess(() => Console.WriteLine("Completed within timeout"));
    /// </code>
    /// </example>
    public LSEventOptions WithTimeout(float timeout) {
        Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Adds a success callback to this event options instance using fluent syntax.
    /// </summary>
    /// <param name="onSuccess">The callback to invoke when the event succeeds.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method adds the callback to the existing success handlers rather than replacing them.
    /// Multiple success callbacks can be registered and will all be invoked upon success.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithSuccess(() => Console.WriteLine("First callback"))
    ///     .WithSuccess(() => Console.WriteLine("Second callback"));
    /// </code>
    /// </example>
    public LSEventOptions WithSuccess(LSAction onSuccess) {
        OnSuccess += onSuccess;
        return this;
    }

    /// <summary>
    /// Sets the success callback for this event options instance using fluent syntax, replacing any existing handlers.
    /// </summary>
    /// <param name="onSuccess">The callback to invoke when the event succeeds.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method replaces all existing success handlers with the new callback.
    /// Use this when you want to ensure only one success handler is registered.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .SetSuccess(() => Console.WriteLine("Only success callback"));
    /// </code>
    /// </example>
    public LSEventOptions SetSuccess(LSAction onSuccess) {
        OnSuccess = onSuccess;
        return this;
    }

    /// <summary>
    /// Adds a failure callback to this event options instance using fluent syntax.
    /// </summary>
    /// <param name="onFailure">The callback to invoke when the event fails.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method adds the callback to the existing failure handlers rather than replacing them.
    /// The callback receives the failure message and a boolean indicating if the event was cancelled.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithFailure((msg) => Console.WriteLine($"Failed: {msg}"));
    /// </code>
    /// </example>
    public LSEventOptions WithFailure(LSAction<string> onFailure) {
        OnFailure += onFailure;
        return this;
    }

    /// <summary>
    /// Sets the failure callback for this event options instance using fluent syntax, replacing any existing handlers.
    /// </summary>
    /// <param name="onFailure">The callback to invoke when the event fails.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method replaces all existing failure handlers with the new callback.
    /// The callback receives the failure message and a boolean indicating if the event was cancelled.
    /// Use this when you want to ensure only one failure handler is registered.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .SetFailure((msg) => Console.WriteLine($"Failed: {msg}"));
    /// </code>
    /// </example>
    public LSEventOptions SetFailure(LSAction<string> onFailure) {
        OnFailure = onFailure;
        return this;
    }

    /// <summary>
    /// Adds a cancellation callback to this event options instance using fluent syntax.
    /// </summary>
    /// <param name="onCancel">The callback to invoke when the event is cancelled.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method adds the callback to the existing cancellation handlers rather than replacing them.
    /// Cancellation callbacks are invoked when the event is explicitly cancelled or times out.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithCancel(() => Console.WriteLine("Event was cancelled"));
    /// </code>
    /// </example>
    public LSEventOptions WithCancel(LSAction onCancel) {
        OnCancel += onCancel;
        return this;
    }

    /// <summary>
    /// Sets the cancellation callback for this event options instance using fluent syntax, replacing any existing handlers.
    /// </summary>
    /// <param name="onCancel">The callback to invoke when the event is cancelled.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method replaces all existing cancellation handlers with the new callback.
    /// Cancellation callbacks are invoked when the event is explicitly cancelled or times out.
    /// Use this when you want to ensure only one cancellation handler is registered.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .SetCancel(() => Console.WriteLine("Event was cancelled"));
    /// </code>
    /// </example>
    public LSEventOptions SetCancel(LSAction onCancel) {
        OnCancel = onCancel;
        return this;
    }

    /// <summary>
    /// Adds an error handler to this event options instance using fluent syntax.
    /// </summary>
    /// <param name="errorHandler">The error handler to add.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method adds the handler to the existing error handlers rather than replacing them.
    /// Error handlers are responsible for processing error messages and returning whether
    /// the error was handled successfully.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithErrorHandler(msg => { 
    ///         Console.Error.WriteLine($"Error: {msg}"); 
    ///         return true; 
    ///     });
    /// </code>
    /// </example>
    public LSEventOptions WithErrorHandler(LSMessageHandler errorHandler) {
        ErrorHandler += errorHandler;
        return this;
    }

    /// <summary>
    /// Sets the error handler for this event options instance using fluent syntax, replacing any existing handlers.
    /// </summary>
    /// <param name="errorHandler">The error handler to set.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method replaces all existing error handlers with the new handler.
    /// Error handlers are responsible for processing error messages and returning whether
    /// the error was handled successfully.
    /// Use this when you want to ensure only one error handler is registered.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .SetErrorHandler(msg => { 
    ///         Console.Error.WriteLine($"Error: {msg}"); 
    ///         return true; 
    ///     });
    /// </code>
    /// </example>
    public LSEventOptions SetErrorHandler(LSMessageHandler errorHandler) {
        ErrorHandler = errorHandler;
        return this;
    }

    /// <summary>
    /// Sets the listener group type for this event options instance using fluent syntax.
    /// </summary>
    /// <param name="groupType">The listener group type to use.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// The group type determines how the dispatcher matches events with listeners.
    /// Static grouping matches by event type only, while subset grouping also considers instances.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithGroupType(ListenerGroupType.SUBSET);
    /// </code>
    /// </example>
    public LSEventOptions WithGroupType(ListenerGroupType groupType) {
        GroupType = groupType;
        return this;
    }

    /// <summary>
    /// Copies callbacks from an LSEvent to this options instance using fluent syntax.
    /// </summary>
    /// <param name="sourceEvent">The event to copy callbacks from.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method sets up the options to forward success and failure calls to the source event.
    /// This is commonly used when creating child events that should signal their parent event.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="sourceEvent"/> is null.</exception>
    /// <example>
    /// <code>
    /// var childOptions = LSEventOptions.Create()
    ///     .CopyCallbacksFrom(parentEvent)
    ///     .WithDispatcher(customDispatcher);
    /// </code>
    /// </example>
    public LSEventOptions CopyCallbacksFrom(LSEvent sourceEvent) {
        if (sourceEvent == null) throw new System.ArgumentNullException(nameof(sourceEvent));

        OnSuccess += sourceEvent.Signal;
        OnFailure += sourceEvent.Failure;
        return this;
    }

    /// <summary>
    /// Copies callbacks from another LSEventOptions instance using fluent syntax.
    /// </summary>
    /// <param name="source">The options instance to copy callbacks from.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// This method adds all callbacks from the source options to this instance.
    /// This is useful for inheriting callback behavior from a base configuration.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <example>
    /// <code>
    /// var derivedOptions = LSEventOptions.Create()
    ///     .CopyCallbacksFrom(baseOptions)
    ///     .WithTimeout(customTimeout);
    /// </code>
    /// </example>
    public LSEventOptions CopyCallbacksFrom(LSEventOptions source) {
        if (source == null) throw new System.ArgumentNullException(nameof(source));

        if (source.OnSuccess != null) OnSuccess += source.OnSuccess;
        if (source.OnFailure != null) OnFailure += source.OnFailure;
        if (source.OnCancel != null) OnCancel += source.OnCancel;
        if (source.OnDispatch != null) OnDispatch += source.OnDispatch;
        if (source.ErrorHandler != null) ErrorHandler += source.ErrorHandler;
        return this;
    }
    #endregion

    #region Static Factory Methods
    /// <summary>
    /// Creates event options configured to chain callbacks to a parent event.
    /// </summary>
    /// <param name="parentEvent">The parent event to chain to.</param>
    /// <param name="dispatcher">Optional custom dispatcher. If null, uses the parent event's dispatcher or default.</param>
    /// <returns>A new <see cref="LSEventOptions"/> instance configured for the parent event.</returns>
    /// <remarks>
    /// This factory method creates options that will automatically forward success and failure
    /// callbacks to the parent event. This is commonly used for creating child operations
    /// that should signal their completion to a parent event.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="parentEvent"/> is null.</exception>
    /// <example>
    /// <code>
    /// var childOptions = LSEventOptions.ForEvent(parentEvent);
    /// var childEvent = SomeOperation.Create(childOptions);
    /// // When childEvent succeeds/fails, it will automatically signal parentEvent
    /// </code>
    /// </example>
    public static LSEventOptions ForEvent(LSEvent parentEvent, LSDispatcher? dispatcher = null) {
        if (parentEvent == null) throw new System.ArgumentNullException(nameof(parentEvent));

        return new LSEventOptions {
            Dispatcher = dispatcher ?? parentEvent.Dispatcher,
            OnSuccess = parentEvent.Signal,
            OnFailure = parentEvent.Failure
        };
    }

    /// <summary>
    /// Creates event options with callbacks from a parent event and base options.
    /// </summary>
    /// <param name="parentEvent">The parent event to chain to.</param>
    /// <param name="baseOptions">Optional base options to inherit settings from.</param>
    /// <returns>A new <see cref="LSEventOptions"/> instance with combined settings.</returns>
    /// <remarks>
    /// This factory method creates options by first copying settings from the base options
    /// (if provided), then adding callbacks to chain to the parent event. This allows
    /// for complex inheritance scenarios.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="parentEvent"/> is null.</exception>
    /// <example>
    /// <code>
    /// var childOptions = LSEventOptions.WithEventCallbacks(parentEvent, standardOptions);
    /// // childOptions inherits settings from standardOptions and chains to parentEvent
    /// </code>
    /// </example>
    public static LSEventOptions WithEventCallbacks(LSEvent parentEvent, LSEventOptions? baseOptions = null) {
        if (parentEvent == null) throw new System.ArgumentNullException(nameof(parentEvent));

        var options = baseOptions != null ? LSEventOptions.Create(baseOptions) : LSEventOptions.Create();
        options.OnSuccess += parentEvent.Signal;
        options.OnFailure += parentEvent.Failure;
        return options;
    }

    /// <summary>
    /// Creates event options with a specific dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use.</param>
    /// <returns>A new <see cref="LSEventOptions"/> instance with the specified dispatcher.</returns>
    /// <remarks>
    /// This factory method provides a convenient way to create options with just a custom dispatcher.
    /// All other settings use their default values.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="dispatcher"/> is null.</exception>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.FromDispatcher(customDispatcher);
    /// </code>
    /// </example>
    public static LSEventOptions FromDispatcher(LSDispatcher dispatcher) {
        if (dispatcher == null) throw new System.ArgumentNullException(nameof(dispatcher));

        return new LSEventOptions { Dispatcher = dispatcher };
    }

    /// <summary>
    /// Creates event options with quick callback setup.
    /// </summary>
    /// <param name="onSuccess">Optional success callback.</param>
    /// <param name="onFailure">Optional failure callback.</param>
    /// <returns>A new <see cref="LSEventOptions"/> instance with the specified callbacks.</returns>
    /// <remarks>
    /// This factory method provides the fastest way to create options with basic callbacks.
    /// All other settings use their default values.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Quick(
    ///     onSuccess: () => Console.WriteLine("Done"),
    ///     onFailure: (msg, cancelled) => Console.WriteLine($"Failed: {msg}")
    /// );
    /// </code>
    /// </example>
    public static LSEventOptions Quick(LSAction? onSuccess = null, LSAction<string>? onFailure = null) {
        return new LSEventOptions(onSuccess, onFailure);
    }
    #endregion

    #region Internal Callback Methods
    /// <summary>
    /// Triggers the dispatch callback if one is registered.
    /// </summary>
    /// <remarks>
    /// This method is called internally when the event is dispatched to listeners.
    /// It's part of the event lifecycle management system.
    /// </remarks>
    internal void dispatch() => OnDispatch?.Invoke();

    /// <summary>
    /// Triggers the success callback if one is registered.
    /// </summary>
    /// <remarks>
    /// This method is called internally when the event completes successfully.
    /// It's part of the event lifecycle management system.
    /// </remarks>
    internal void success() => OnSuccess?.Invoke();

    /// <summary>
    /// Triggers the failure callback with the specified message if one is registered.
    /// </summary>
    /// <param name="msg">The failure message to pass to the callback.</param>
    /// <param name="cancel">Indicates whether the failure caused cancellation.</param>
    /// <remarks>
    /// This method is called internally when the event encounters a failure.
    /// It's part of the event lifecycle management system.
    /// </remarks>
    internal void failure(string msg) => OnFailure?.Invoke(msg);

    /// <summary>
    /// Triggers the cancellation callback if one is registered.
    /// </summary>
    /// <remarks>
    /// This method is called internally when the event is cancelled.
    /// It's part of the event lifecycle management system.
    /// </remarks>
    internal void cancel() => OnCancel?.Invoke();

    /// <summary>
    /// Handles error conditions using the registered error handler.
    /// </summary>
    /// <param name="msg">The error message to handle.</param>
    /// <returns>
    /// <c>true</c> if the error was handled successfully; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="LSException">
    /// Thrown when no error handler is registered.
    /// </exception>
    /// <remarks>
    /// This method provides a centralized way to handle errors that occur during event processing.
    /// If no error handler is registered, an exception is thrown with the error details.
    /// </remarks>
    public bool Error(string? msg) {
        if (ErrorHandler == null) {
            throw new LSException($"no_error_handler:{msg}");
        }
        return ErrorHandler(msg);
    }
    #endregion
}

/// <summary>
/// Extension methods for <see cref="LSEventOptions"/> to provide additional fluent API capabilities.
/// </summary>
/// <remarks>
/// These extension methods provide additional functionality for working with event options,
/// including chaining operations, type conversions, and inheritance patterns.
/// </remarks>
public static class LSEventOptionsExtensions {
    /// <summary>
    /// Configures the event options to chain callbacks to a target event.
    /// </summary>
    /// <param name="source">The event options to configure.</param>
    /// <param name="targetEvent">The event to chain to.</param>
    /// <returns>The source options instance for method chaining.</returns>
    /// <remarks>
    /// This extension method adds callbacks to the source options that will forward
    /// success and failure notifications to the target event. This is useful for
    /// creating event hierarchies or dependency chains.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="source"/> or <paramref name="targetEvent"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var options = LSEventOptions.Create()
    ///     .WithTimeout(5.0f)
    ///     .ChainTo(parentEvent);
    /// </code>
    /// </example>
    public static LSEventOptions ChainTo(this LSEventOptions source, LSEvent targetEvent) {
        if (source == null) throw new System.ArgumentNullException(nameof(source));
        if (targetEvent == null) throw new System.ArgumentNullException(nameof(targetEvent));

        source.OnSuccess += targetEvent.Signal;
        source.OnFailure += targetEvent.Failure;
        return source;
    }

    /// <summary>
    /// Inherits dispatcher and error handler settings from another options instance.
    /// </summary>
    /// <param name="target">The options instance to configure.</param>
    /// <param name="source">The options instance to inherit from.</param>
    /// <returns>The target options instance for method chaining.</returns>
    /// <remarks>
    /// This extension method copies the dispatcher and error handler from the source
    /// to the target, allowing for inheritance of infrastructure settings without
    /// copying all callbacks.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="target"/> or <paramref name="source"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var childOptions = LSEventOptions.Create()
    ///     .InheritFrom(parentOptions)
    ///     .WithSuccess(() => Console.WriteLine("Child completed"));
    /// </code>
    /// </example>
    public static LSEventOptions InheritFrom(this LSEventOptions target, LSEventOptions source) {
        if (target == null) throw new System.ArgumentNullException(nameof(target));
        if (source == null) throw new System.ArgumentNullException(nameof(source));

        target.Dispatcher = source.Dispatcher;
        target.CopyCallbacksFrom(source);
        return target;
    }

    /// <summary>
    /// Converts <see cref="LSEventOptions"/> to <see cref="LSEventIOptions"/> for instance-based events.
    /// </summary>
    /// <param name="source">The source options to convert.</param>
    /// <returns>A new <see cref="LSEventIOptions"/> instance with copied settings.</returns>
    /// <remarks>
    /// This extension method creates a new instance event options object that inherits
    /// all settings from the source but uses subset-based listener grouping appropriate
    /// for instance events.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <example>
    /// <code>
    /// var staticOptions = LSEventOptions.Quick(() => Console.WriteLine("Done"));
    /// var instanceOptions = staticOptions.ToInstanceOptions();
    /// // instanceOptions has the same callbacks but uses SUBSET grouping
    /// </code>
    /// </example>
    public static LSEventIOptions ToInstanceOptions(this LSEventOptions source) {
        if (source == null) throw new System.ArgumentNullException(nameof(source));

        return LSEventIOptions.Create(source);
    }
}
