namespace LSUtils;

/// <summary>
/// Configuration options for instance-based events that use subset listener grouping.
/// </summary>
/// <remarks>
/// This class extends <see cref="LSEventOptions"/> to provide default configuration
/// specifically for events that are associated with instances and use subset-based
/// listener matching. The group type is set to <see cref="ListenerGroupType.SUBSET"/>
/// by default, which is appropriate for events tied to specific object instances.
/// 
/// <para>
/// Instance events are commonly used when you need to listen for events from specific
/// object instances rather than all events of a particular type. This class automatically
/// configures the appropriate listener grouping strategy.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Basic instance event options
/// var options = LSEventIOptions.Create();
/// 
/// // Copy dispatcher and error handler from base options but ensure subset grouping
/// var options = LSEventIOptions.Create(baseOptions);
/// 
/// // Factory method for chaining to parent events
/// var childOptions = LSEventIOptions.ForEvent(parentEvent);
/// 
/// // Create with specific dispatcher and error handler
/// var options = LSEventIOptions.Create(customDispatcher, errorHandler);
/// </code>
/// </example>
public class LSEventIOptions : LSEventOptions {
    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventIOptions"/> class with default settings.
    /// </summary>
    /// <remarks>
    /// Creates event options with subset-based listener grouping, which is appropriate
    /// for events that are associated with specific instances.
    /// </remarks>
    protected LSEventIOptions() : base() {
        GroupType = ListenerGroupType.SUBSET;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventIOptions"/> class by copying from another options instance.
    /// </summary>
    /// <param name="copyFrom">The source options to copy settings from.</param>
    /// <remarks>
    /// Creates a copy of the source options but ensures the group type is set to SUBSET,
    /// which is appropriate for instance-based events.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="copyFrom"/> is null.</exception>
    protected LSEventIOptions(LSEventOptions copyFrom) : base(copyFrom) {
        GroupType = ListenerGroupType.SUBSET; // Override for instance events
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventIOptions"/> class with a specific dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for event handling. If null, uses the default instance.</param>
    /// <param name="onSuccess">Optional callback to invoke when the event succeeds.</param>
    /// <remarks>
    /// Creates instance event options with a custom dispatcher and optional success callback.
    /// The group type is automatically set to SUBSET for instance-based listener matching.
    /// </remarks>
    protected LSEventIOptions(LSDispatcher dispatcher, LSAction? onSuccess = null) : base(dispatcher, onSuccess) {
        GroupType = ListenerGroupType.SUBSET;
    }

    protected LSEventIOptions(LSDispatcher? dispatcher, LSMessageHandler? errorHandler) { 
        Dispatcher = dispatcher ?? LSDispatcher.Instance;
        ErrorHandler += errorHandler;
        GroupType = ListenerGroupType.SUBSET; // Ensure subset grouping for instance events
    }
    #endregion

    #region Static Create Methods
    /// <summary>
    /// Creates a new instance of the <see cref="LSEventIOptions"/> class with default settings.
    /// </summary>
    /// <returns>A new LSEventIOptions instance with default settings and subset grouping.</returns>
    /// <remarks>
    /// Creates event options with subset-based listener grouping, which is appropriate
    /// for events that are associated with specific instances.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventIOptions.Create();
    /// </code>
    /// </example>
    public static new LSEventIOptions Create() {
        return new LSEventIOptions();
    }

    /// <summary>
    /// Creates a new instance of the <see cref="LSEventIOptions"/> class by copying from another options instance.
    /// </summary>
    /// <param name="copyFrom">The source options to copy settings from.</param>
    /// <returns>A new LSEventIOptions instance with copied settings and subset grouping.</returns>
    /// <remarks>
    /// Creates a copy of the source options but ensures the group type is set to SUBSET,
    /// which is appropriate for instance-based events.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="copyFrom"/> is null.</exception>
    /// <example>
    /// <code>
    /// var options = LSEventIOptions.Create(baseOptions);
    /// </code>
    /// </example>
    public static new LSEventIOptions Create(LSEventOptions copyFrom) {
        return new LSEventIOptions(copyFrom);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="LSEventIOptions"/> class with a specific dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for event handling. If null, uses the default instance.</param>
    /// <param name="onSuccess">Optional callback to invoke when the event succeeds.</param>
    /// <returns>A new LSEventIOptions instance with the specified dispatcher and subset grouping.</returns>
    /// <remarks>
    /// Creates instance event options with a custom dispatcher and optional success callback.
    /// The group type is automatically set to SUBSET for instance-based listener matching.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventIOptions.Create(customDispatcher, () => Console.WriteLine("Success"));
    /// </code>
    /// </example>
    public static new LSEventIOptions Create(LSDispatcher dispatcher, LSAction? onSuccess = null) {
        return new LSEventIOptions(dispatcher, onSuccess);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="LSEventIOptions"/> class with a specific dispatcher and error handler.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for event handling. If null, uses the default instance.</param>
    /// <param name="errorHandler">The error handler to use for processing error messages.</param>
    /// <returns>A new LSEventIOptions instance with the specified dispatcher and error handler.</returns>
    /// <example>
    /// <code>
    /// var options = LSEventIOptions.Create(customDispatcher, msg => { Console.Error.WriteLine(msg); return true; });
    /// </code>
    /// </example>
    public static LSEventIOptions Create(LSDispatcher? dispatcher, LSMessageHandler? errorHandler) {
        return new LSEventIOptions(dispatcher, errorHandler);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the listener group type for instance-based events.
    /// </summary>
    /// <value>
    /// The group type used for listener matching. Defaults to <see cref="ListenerGroupType.SUBSET"/>.
    /// </value>
    /// <remarks>
    /// Overrides the base class default to use subset grouping, which is more appropriate
    /// for events that are tied to specific object instances rather than global events.
    /// While this can be changed, it's recommended to keep it as SUBSET for instance events.
    /// </remarks>
    public override ListenerGroupType GroupType { get; set; } = ListenerGroupType.SUBSET;
    #endregion

    #region Fluent API Methods Override
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
    /// var options = LSEventIOptions.Create()
    ///     .WithDispatcher(customDispatcher)
    ///     .WithTimeout(5.0f);
    /// </code>
    /// </example>
    public new LSEventIOptions WithDispatcher(LSDispatcher? dispatcher) {
        Dispatcher = dispatcher ?? LSDispatcher.Instance;
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
    /// var options = LSEventIOptions.Create()
    ///     .WithDispatch(() => Console.WriteLine("Event dispatched"));
    /// </code>
    /// </example>
    public new LSEventIOptions WithDispatch(LSAction onDispatch) {
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
    /// var options = LSEventIOptions.Create()
    ///     .SetDispatch(() => Console.WriteLine("Event dispatched"));
    /// </code>
    /// </example>
    public new LSEventIOptions SetDispatch(LSAction onDispatch) {
        base.SetDispatch(onDispatch);
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
    /// var options = LSEventIOptions.Create()
    ///     .WithTimeout(10.0f)
    ///     .WithSuccess(() => Console.WriteLine("Completed within timeout"));
    /// </code>
    /// </example>
    public new LSEventIOptions WithTimeout(float timeout) {
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
    /// var options = LSEventIOptions.Create()
    ///     .WithSuccess(() => Console.WriteLine("First callback"))
    ///     .WithSuccess(() => Console.WriteLine("Second callback"));
    /// </code>
    /// </example>
    public new LSEventIOptions WithSuccess(LSAction onSuccess) {
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
    /// var options = LSEventIOptions.Create()
    ///     .SetSuccess(() => Console.WriteLine("Only success callback"));
    /// </code>
    /// </example>
    public new LSEventIOptions SetSuccess(LSAction onSuccess) {
        base.SetSuccess(onSuccess);
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
    /// var options = LSEventIOptions.Create()
    ///     .WithFailure((msg) => Console.WriteLine($"Failed: {msg}"));
    /// </code>
    /// </example>
    public new LSEventIOptions WithFailure(LSAction<string> onFailure) {
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
    /// var options = LSEventIOptions.Create()
    ///     .SetFailure((msg) => Console.WriteLine($"Failed: {msg}"));
    /// </code>
    /// </example>
    public new LSEventIOptions SetFailure(LSAction<string> onFailure) {
        base.SetFailure(onFailure);
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
    /// var options = LSEventIOptions.Create()
    ///     .WithCancel(() => Console.WriteLine("Event was cancelled"));
    /// </code>
    /// </example>
    public new LSEventIOptions WithCancel(LSAction onCancel) {
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
    /// var options = LSEventIOptions.Create()
    ///     .SetCancel(() => Console.WriteLine("Event was cancelled"));
    /// </code>
    /// </example>
    public new LSEventIOptions SetCancel(LSAction onCancel) {
        base.SetCancel(onCancel);
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
    /// var options = LSEventIOptions.Create()
    ///     .WithErrorHandler(msg => { 
    ///         Console.Error.WriteLine($"Error: {msg}"); 
    ///         return true; 
    ///     });
    /// </code>
    /// </example>
    public new LSEventIOptions WithErrorHandler(LSMessageHandler errorHandler) {
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
    /// var options = LSEventIOptions.Create()
    ///     .SetErrorHandler(msg => { 
    ///         Console.Error.WriteLine($"Error: {msg}"); 
    ///         return true; 
    ///     });
    /// </code>
    /// </example>
    public new LSEventIOptions SetErrorHandler(LSMessageHandler errorHandler) {
        base.SetErrorHandler(errorHandler);
        return this;
    }

    /// <summary>
    /// Sets the listener group type for this event options instance using fluent syntax.
    /// </summary>
    /// <param name="groupType">The listener group type to use.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <remarks>
    /// The group type determines how the dispatcher matches events with listeners.
    /// For instance events, SUBSET grouping is recommended, but this can be overridden if needed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = LSEventIOptions.Create()
    ///     .WithGroupType(ListenerGroupType.SINGLE);
    /// </code>
    /// </example>
    public new LSEventIOptions WithGroupType(ListenerGroupType groupType) {
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
    /// var childOptions = LSEventIOptions.Create()
    ///     .CopyCallbacksFrom(parentEvent)
    ///     .WithDispatcher(customDispatcher);
    /// </code>
    /// </example>
    public new LSEventIOptions CopyCallbacksFrom(LSEvent sourceEvent) {
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
    /// var derivedOptions = LSEventIOptions.Create()
    ///     .CopyCallbacksFrom(baseOptions)
    ///     .WithTimeout(customTimeout);
    /// </code>
    /// </example>
    public new LSEventIOptions CopyCallbacksFrom(LSEventOptions source) {
        base.CopyCallbacksFrom(source);
        return this;
    }
    #endregion

    #region Static Factory Methods
    /// <summary>
    /// Creates instance event options configured to chain callbacks to a parent event.
    /// </summary>
    /// <param name="parentEvent">The parent event to chain to.</param>
    /// <param name="baseOptions">Optional base options to inherit settings from.</param>
    /// <returns>A new <see cref="LSEventIOptions"/> instance configured for the parent event.</returns>
    /// <remarks>
    /// This factory method creates instance event options that will automatically forward
    /// success and failure callbacks to the parent event. The group type is automatically
    /// set to SUBSET for proper instance-based listener matching.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="parentEvent"/> is null.</exception>
    /// <example>
    /// <code>
    /// var childOptions = LSEventIOptions.ForEvent(parentEvent, baseOptions);
    /// var childEvent = SomeInstanceOperation.Create(specificInstance, childOptions);
    /// // When childEvent succeeds/fails, it will automatically signal parentEvent
    /// </code>
    /// </example>
    public static LSEventIOptions ForEvent(LSEvent parentEvent, LSEventOptions? baseOptions = null) {
        if (parentEvent == null) throw new System.ArgumentNullException(nameof(parentEvent));

        var options = baseOptions != null ? LSEventIOptions.Create(baseOptions) : LSEventIOptions.Create();
        options.OnSuccess += parentEvent.Signal;
        options.OnFailure += parentEvent.Failure;
        return options;
    }
    #endregion
}
