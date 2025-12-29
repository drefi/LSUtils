namespace LSUtils.ProcessSystem;

/// <summary>
/// Generic version of LSProcessSession that provides strongly-typed access to the process instance.
/// Eliminates casting overhead and provides compile-time type safety for process-specific operations.
/// </summary>
/// <typeparam name="TProcess">The specific process type being executed in this session.</typeparam>
/// <remarks>
/// <b>Type Safety Benefits:</b><br/>
/// • Eliminates runtime casting in handlers and conditions<br/>
/// • Provides IntelliSense support for process-specific properties<br/>
/// • Catches type mismatches at compile time<br/>
/// • Improves code readability and maintainability<br/>
/// <br/>
/// <b>Usage Pattern:</b><br/>
/// This generic session is typically created internally by the processing system when executing
/// strongly-typed handlers and conditions, providing seamless type safety without additional overhead.
/// </remarks>
public class LSProcessSession<TProcess> : LSProcessSession
    where TProcess : LSProcess
{
    /// <summary>
    /// The strongly-typed process being executed by this session.
    /// Provides direct access to process-specific properties without casting.
    /// </summary>
    /// <value>The TProcess instance being executed. This property provides compile-time type safety.</value>
    /// <remarks>
    /// <b>Type Safety:</b><br/>
    /// This property returns the process as its concrete type, eliminating the need for casting
    /// and providing full IntelliSense support for process-specific members.
    /// </remarks>
    public new TProcess Process => (TProcess)base.Process;

    /// <summary>
    /// Initializes a new strongly-typed processing session for the specified process and root node.
    /// </summary>
    /// <param name="manager">The process manager coordinating this session.</param>
    /// <param name="process">The strongly-typed process to be executed.</param>
    /// <param name="rootNode">The root node that will coordinate processing.</param>
    /// <param name="instances">Optional processable instances for context.</param>
    /// <remarks>
    /// <b>Internal Construction:</b><br/>
    /// This constructor is internal as strongly-typed sessions are typically created by the processing
    /// system infrastructure when executing generic handlers and conditions.
    /// </remarks>
    internal LSProcessSession(LSProcessManager manager, TProcess process, ILSProcessNode rootNode, params ILSProcessable[]? instances)
        : base(manager, process, rootNode, instances)
    {
    }
}
