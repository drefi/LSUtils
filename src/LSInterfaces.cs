namespace LSUtils;
/// <summary>
/// Interface for eventable entities.
/// </summary>
public interface ILSEventable {

    /// <summary>
    /// Gets the class name of the eventable.
    /// </summary>
    string ClassName { get; }

    /// <summary>
    /// Gets the ID of the eventable.
    /// </summary>
    System.Guid ID { get; }

    /// <summary>
    /// Initializes the eventable, allowing callbacks for dispatching events with optional success and failure callbacks.
    /// Standard implementation for use with OnInitializeEvent.
    /// </summary>
    /// <param name="onSuccess">Callback to execute on successful initialization.</param>
    /// <param name="onFailure">Handler for failure scenarios.</param>
    /// <param name="dispatcher">The dispatcher to use for event handling. If null, the default dispatcher is used.</param>
    bool Initialize(LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null);
    //void Cleanup();
    //OnCleanupEvent Cleanup(LSAction onSuccess, LSMessageHandler onFailure, LSDispatcher dispatcher);
}

public interface ILSContext : ILSEventable {
    void AddState<TState>(TState state, LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) where TState : ILSState;
    TState GetState<TState>() where TState : ILSState;
    bool TryGetState<TState>(out TState state) where TState : ILSState;
    void SetState<TState>(LSAction<TState>? enterCallback = null, LSAction<TState>? exitCallback = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) where TState : ILSState;
}
public interface ILSState : ILSEventable {
    void Enter<TState>(LSAction<TState>? enterCallback = null, LSAction<TState>? exitCallback = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) where TState : ILSState;
    void Exit(LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null);
}
