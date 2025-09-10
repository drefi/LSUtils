namespace LSUtils.EventSystem;

/// <summary>
/// Configuration options for event processing and initialization.
/// Used to pass common parameters to event creation and processing methods.
/// </summary>
public class LSLegacyEventOptions {
    /// <summary>
    /// The dispatcher to use for event processing. Defaults to the singleton instance.
    /// </summary>
    public LSLegacyDispatcher Dispatcher { get; set; } = LSLegacyDispatcher.Singleton;

    /// <summary>
    /// The owner instance associated with the event, if any.
    /// </summary>
    public object? OwnerInstance { get; set; }

    /// <summary>
    /// Callback to execute when the event completes successfully.
    /// </summary>
    public LSAction<ILSEvent>? OnSuccessCallback { get; set; }

    /// <summary>
    /// Callback to execute when the event fails but processing continues.
    /// </summary>
    public LSAction<ILSEvent>? OnFailureCallback { get; set; }

    /// <summary>
    /// Callback to execute when the event is cancelled.
    /// </summary>
    public LSAction<ILSEvent>? OnCancelCallback { get; set; }

    /// <summary>
    /// Callback to execute when event processing completes (regardless of outcome).
    /// </summary>
    public LSAction<ILSEvent>? OnCompleteCallback { get; set; }

    /// <summary>
    /// Initializes a new instance with default values.
    /// </summary>
    public LSLegacyEventOptions() { }

    /// <summary>
    /// Initializes a new instance with the specified dispatcher and owner.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for event processing.</param>
    /// <param name="ownerInstance">The owner instance associated with the event.</param>
    public LSLegacyEventOptions(LSLegacyDispatcher dispatcher, object? ownerInstance = null) {
        Dispatcher = dispatcher;
        OwnerInstance = ownerInstance;
    }

    /// <summary>
    /// Sets the dispatcher for event processing.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use.</param>
    /// <returns>This instance for method chaining.</returns>
    public LSLegacyEventOptions WithDispatcher(LSLegacyDispatcher dispatcher) {
        Dispatcher = dispatcher;
        return this;
    }

    /// <summary>
    /// Sets the owner instance associated with the event.
    /// </summary>
    /// <param name="ownerInstance">The owner instance.</param>
    /// <returns>This instance for method chaining.</returns>
    public LSLegacyEventOptions WithOwnerInstance(object? ownerInstance) {
        OwnerInstance = ownerInstance;
        return this;
    }

    /// <summary>
    /// Adds a success callback to be executed when the event completes successfully.
    /// </summary>
    /// <param name="callback">The callback to add.</param>
    /// <returns>This instance for method chaining.</returns>
    public LSLegacyEventOptions WithSuccessCallback(LSAction<ILSEvent>? callback) {
        OnSuccessCallback += callback;
        return this;
    }

    /// <summary>
    /// Adds a failure callback to be executed when the event fails.
    /// </summary>
    /// <param name="callback">The callback to add.</param>
    /// <returns>This instance for method chaining.</returns>
    public LSLegacyEventOptions WithFailureCallback(LSAction<ILSEvent>? callback) {
        OnFailureCallback += callback;
        return this;
    }

    /// <summary>
    /// Adds a cancel callback to be executed when the event is cancelled.
    /// </summary>
    /// <param name="callback">The callback to add.</param>
    /// <returns>This instance for method chaining.</returns>
    public LSLegacyEventOptions WithCancelCallback(LSAction<ILSEvent>? callback) {
        OnCancelCallback += callback;
        return this;
    }

    /// <summary>
    /// Adds a complete callback to be executed when event processing completes.
    /// </summary>
    /// <param name="callback">The callback to add.</param>
    /// <returns>This instance for method chaining.</returns>
    public LSLegacyEventOptions WithCompleteCallback(LSAction<ILSEvent>? callback) {
        OnCompleteCallback += callback;
        return this;
    }

    /// <summary>
    /// Creates a new instance of the specified options type with the given parameters.
    /// </summary>
    /// <typeparam name="TOptions">The specific options type to create.</typeparam>
    /// <param name="dispatcher">The dispatcher to use, or null to use the singleton.</param>
    /// <param name="ownerInstance">The owner instance to associate with the event.</param>
    /// <returns>A new instance of the specified options type.</returns>
    public static TOptions Create<TOptions>(LSLegacyDispatcher? dispatcher = null, object? ownerInstance = null)
        where TOptions : LSLegacyEventOptions, new() {
        return new TOptions() {
            Dispatcher = dispatcher ?? LSLegacyDispatcher.Singleton,
            OwnerInstance = ownerInstance
        };
    }
}
