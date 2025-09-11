using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Configuration for events in the LSEventSystem
/// 
/// LSEventOptions serves as the central configuration point for event processing.
/// 
/// Key Features:
/// - Owner instance tracking for context-aware processing
/// 
/// Configuration Patterns:
/// - Using LSDispatch.Singleton: new LSEventOptions()
/// - With custom dispatcher: new LSEventOptions(dispatcher)
/// - With Owner: new LSEventOptions(dispatcher, ownerInstance)
/// 
/// Thread Safety:
/// - Instance is immutable after construction
/// </summary>
public class LSEventOptions {
    /// <summary>
    /// The dispatcher instance used for event processing and handler retrieval.
    /// 
    /// This dispatcher will be used to:
    /// - Retrieve globally registered handlers for event types
    /// - Process events through the state machine
    /// - Coordinate phase and state transitions
    /// 
    /// If not provided in constructor, defaults to LSDispatcher.Singleton.
    /// </summary>
    public LSDispatcher Dispatcher { get; init; }
    internal readonly List<IHandlerEntry> _entries = new();
    public IReadOnlyList<IHandlerEntry> Entries => _entries.AsReadOnly();

    /// <summary>
    /// The object instance that owns or is associated with events created with these options.
    /// 
    /// Common use cases:
    /// - Component initialization: Pass the component being initialized
    /// - State management: Pass the state machine or controller
    /// - Context tracking: Pass any object that provides context for event processing
    /// 
    /// This value is accessible to event handlers through the LSEventOptions instance
    /// and can be used for context-aware processing and ownership validation.
    /// </summary>
    public object? OwnerInstance { get; protected set; }

    /// <summary>
    /// Initializes a new instance of LSEventOptions with the specified configuration.
    /// 
    /// This constructor provides the primary way to configure event processing options
    /// including dispatcher assignment and owner instance tracking. All other configuration
    /// is done through the fluent API methods.
    /// </summary>
    /// <param name="dispatcher">
    /// The dispatcher to use for event processing. If null, defaults to LSDispatcher.Singleton.
    /// The dispatcher handles global handler retrieval and event processing coordination.
    /// </param>
    /// <param name="ownerInstance">
    /// Optional object that owns or is associated with events created using these options.
    /// Commonly used for component initialization and context tracking.
    /// </param>
    public LSEventOptions(LSDispatcher? dispatcher = null, object? ownerInstance = null) {
        Dispatcher = dispatcher ?? LSDispatcher.Singleton;
        OwnerInstance = ownerInstance;
    }
}

public class LSEventOptions<TEvent> : LSEventOptions where TEvent : ILSEvent {
    public LSEventOptions(LSDispatcher? dispatcher = null, object? ownerInstance = null) : base(dispatcher, ownerInstance) { }
    public LSEventOptions<TEvent> WithCallback(System.Func<LSEventRegister<TEvent>, LSEventRegister<TEvent>> configureRegister) {
        var register = configureRegister(new LSEventRegister<TEvent>());
        _entries.AddRange(register.GetEntries());
        return this;
    }
    public LSEventOptions<TEvent> WithPhaseHandler<TPhase>(System.Func<LSPhaseHandlerRegister<TEvent, TPhase>, LSPhaseHandlerRegister<TEvent, TPhase>> config) where TPhase : LSEventBusinessState.PhaseState {
        var register = config(new LSPhaseHandlerRegister<TEvent, TPhase>());
        var entry = register.Build();
        if (entry == null) throw new LSArgumentNullException(nameof(entry));
        _entries.Add(entry);
        return this;
    }
    public LSEventOptions<TEvent> WithStateHandler<TState>(System.Func<LSStateHandlerRegister<TEvent, TState>, LSStateHandlerRegister<TEvent, TState>> config) where TState : IEventProcessState {
        var register = config(new LSStateHandlerRegister<TEvent, TState>());
        var entry = register.Build();
        if (entry == null) throw new LSArgumentNullException(nameof(entry));
        _entries.Add(entry);
        return this;
    }
}
