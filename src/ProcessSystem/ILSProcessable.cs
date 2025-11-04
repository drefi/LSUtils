namespace LSUtils.ProcessSystem;
/// <summary>
/// Defines entities that can serve as context targets for process execution within the LSProcessSystem.
/// <para>
/// ILSProcessable entities can have instance-specific processing contexts registered with the
/// LSProcessManager, allowing different behavior based on the target entity. This enables
/// entity-aware processing where the same process type can behave differently depending
/// on what it's operating on.
/// </para>
/// <para>
/// <b>Context Targeting Priority:</b><br/>
/// When a process executes with an ILSProcessable instance, the system looks up registered
/// contexts in this priority order:<br/>
/// 1. Process-local contexts (via WithProcessing)<br/>
/// 2. Instance-specific contexts (registered for this exact entity)<br/>
/// 3. Global contexts (registered for the process type)
/// </para>
/// <para>
/// <b>Entity Lifecycle:</b><br/>
/// - Entity created with unique, persistent ID<br/>
/// - Initialize() called to register instance-specific processing logic<br/>
/// - Entity serves as target for process execution throughout its lifetime<br/>
/// - Multiple processes can target the same entity with different behaviors
/// </para>
/// </summary>
/// <example>
/// Basic entity initialization with spatial delegate setup:
/// <code>
/// var entity = new GameEntity(Guid.NewGuid(), "Player");
/// ILSVector2 position = new Vector2(10, 20);
/// GetPosition getPos = () => position;
/// LSAction&lt;ILSVector2&gt; setPos = (newPos) => position = newPos;
/// 
/// entity.Initialize(init => init
///     .Handler("positionDelegate", session => {
///         session.Process.SetData("getPosition", getPos);
///         session.Process.SetData("setPosition", setPos);
///         return LSProcessResultStatus.SUCCESS;
///     })
/// );
/// 
/// var process = new GameActionProcess();
/// process.Execute(entity); // Uses entity-specific context
/// </code>
/// </example>
public interface ILSProcessable {
    public const string INITIALIZE_LABEL = "initialize";
    public const string ID_LABEL = "id";
    /// <summary>
    /// Unique identifier for this processable entity instance.
    /// 
    /// Must remain constant throughout the entity's lifetime and should be unique
    /// across all ILSProcessable instances in the system. Used by LSProcessManager
    /// for instance-specific context lookup and association.
    /// </summary>
    public System.Guid ID { get; }
    
    /// <summary>
    /// Registers instance-specific processing logic with the LSProcessManager.
    /// <para>
    /// Creates a processing context that will be used whenever processes target this
    /// specific entity instance. The provided builder defines node hierarchies that
    /// handle entity-specific operations, typically through selector/sequence patterns.
    /// </para>
    /// <para>
    /// The initBuilder parameter allows the caller to provide initialization-specific logic
    /// that will be executed during the initialization process, commonly used for setting up
    /// delegates, data, and entity-specific handlers.
    /// </para>
    /// </summary>
    /// <param name="initBuilder">Builder action to define instance-specific processing logic during initialization</param>
    /// <param name="manager">Process manager for registration (uses singleton if null)</param>
    /// <returns>SUCCESS if registration completed, FAILURE if registration failed</returns>
    /// <example>
    /// Typical implementation pattern from WHEntity:
    /// <code>
    /// public LSProcessResultStatus Initialize(LSProcessBuilderAction? initBuilderAction = null, LSProcessManager? manager = null) {
    ///     _manager = manager ?? LSProcessManager.Singleton;
    ///     var initializeProcess = new WHEntityProcess(this, INITIALIZE_LABEL);
    ///     
    ///     return initializeProcess
    ///         .WithProcessing(root => root
    ///             .Sequence(INITIALIZE_LABEL, main => main
    ///                 .Handler("isInitialized", session => {
    ///                     if (IsInitialized) return LSProcessResultStatus.FAILURE;
    ///                     return LSProcessResultStatus.SUCCESS;
    ///                 })
    ///                 .Sequence("initializeBuilderAction", initBuilderAction)
    ///                 .Handler("setSpatialDelegates", session => {
    ///                     // Extract delegates from process data
    ///                     session.Process.TryGetData("getPosition", out _getPosition);
    ///                     session.Process.TryGetData("setPosition", out _setPosition);
    ///                     IsInitialized = true;
    ///                     return LSProcessResultStatus.SUCCESS;
    ///                 })
    ///             )
    ///         ).Execute(this);
    /// }
    /// </code>
    /// </example>
    LSProcessResultStatus Initialize(LSProcessBuilderAction? initBuilder = null, LSProcessManager? manager = null, params ILSProcessable[]? forwardProcessables);
}
