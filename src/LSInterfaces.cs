namespace LSUtils;

public interface ILSClass {
    /// <summary>
    /// Gets the class name of the eventable.
    /// </summary>
    string ClassName { get; }
}

public interface ILSSerializer {
    /// <summary>
    /// Serializes the given object to a string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A string representation of the serialized object.</returns>
    string Serialize<T>(T obj);

    /// <summary>
    /// Deserializes a string back into an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize into.</typeparam>
    /// <param name="data">The string data to deserialize.</param>
    /// <returns>An object of type T.</returns>
    T Deserialize<T>(string data);
}
public interface ILSSerializable : ILSClass {
    /// <summary>
    /// Saves the object to a string using the provided serializer.
    /// </summary>
    /// <param name="serializer">The serializer to use for saving.</param>
    /// <returns>A string representation of the saved object.</returns>
    string Save(ILSSerializer serializer);
}
/// <summary>
/// Interface for eventable entities.
/// </summary>
public interface ILSEventable : ILSClass {

    /// <summary>
    /// Gets the ID of the eventable.
    /// </summary>
    System.Guid ID { get; }

    /// <summary>
    /// Initializes the eventable, allowing callbacks for dispatching events with optional success and failure callbacks.
    /// Standard implementation for use with OnInitializeEvent.
    /// </summary>
    /// <param name="eventOptions">event options for initialization.</param>
    void Initialize(LSEventIOptions eventOptions);
    void Cleanup();
}

public interface ILSContext : ILSEventable {
    void AddState<TState>(TState state) where TState : ILSState;
    TState GetState<TState>() where TState : ILSState;
    bool TryGetState<TState>(out TState state) where TState : ILSState;
    void SetState<TState>(LSAction<TState>? enterCallback = null, LSAction<TState>? exitCallback = null, LSEventIOptions? eventOptions = null) where TState : ILSState;
}
public interface ILSState : ILSEventable {
    void Enter<TState>(LSAction<TState> enterCallback, LSAction<TState> exitCallback, LSEventIOptions eventOptions) where TState : ILSState;
    void Exit(LSEventIOptions eventOptions);
}
