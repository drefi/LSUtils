namespace LSUtils.ProcessSystem;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;

/// <summary>
/// Central registry and orchestrator for processing contexts in the LSProcessSystem.
/// <para>
/// The LSProcessManager maintains a two-level dictionary structure that maps process types
/// to processable instances (or global contexts), storing registered node hierarchies that
/// define processing behavior. During process execution, it merges contexts from multiple
/// sources to create the final processing tree.
/// </para>
/// <para>
/// <b>Core Architecture:</b><br/>
/// - Thread-safe ConcurrentDictionary storage for registered contexts<br/>
/// - Singleton pattern for system-wide access and coordination<br/>
/// - Context cloning to preserve original registrations during merging<br/>
/// - Three-tier priority system for context resolution
/// </para>
/// <para>
/// <b>Context Priority Order (highest to lowest):</b><br/>
/// 1. Local contexts (provided via WithProcessing)<br/>
/// 2. Instance-specific contexts (registered for specific ILSProcessable instances)<br/>
/// 3. Global contexts (registered without specific instance)
/// </para>
/// <para>
/// <b>Internal Storage:</b><br/>
/// Uses GlobalProcessable.Instance as a sentinel key for global contexts,
/// distinguishing them from instance-specific registrations in the same dictionary.
/// </para>
/// </summary>
/// <example>
/// Registering and using different context levels:
/// <code>
/// // Register global context for all MockProcess instances
/// LSProcessManager.Singleton.Register&lt;MockProcess&gt;(builder => builder
///     .Sequence("global-sequence", seq => seq
///         .Handler("global-handler1", session => LSProcessResultStatus.SUCCESS)
///         .Handler("global-handler2", session => LSProcessResultStatus.SUCCESS)
///     )
/// );
/// 
/// // Register instance-specific context
/// var entity = new GameEntity(Guid.NewGuid(), "Player");
/// LSProcessManager.Singleton.Register&lt;MockProcess&gt;(builder => builder
///     .Handler("player-specific-logic", session => LSProcessResultStatus.SUCCESS),
///     instance: entity
/// );
/// 
/// // Process execution merges all applicable contexts
/// var process = new MockProcess();
/// process.WithProcessing(builder => builder  // Local context (highest priority)
///     .Handler("local-validation", session => LSProcessResultStatus.SUCCESS)
/// );
/// 
/// var result = process.Execute(entity); // Merges local + instance + global contexts
/// </code>
/// </example>
public class LSProcessManager {
    public const string ClassName = nameof(LSProcessManager);
    public static LSProcessManager Singleton { get; } = new LSProcessManager();

    public static void DebugLogging(bool status = true, LSLogLevel level = LSLogLevel.DEBUG) {
        LSLogger.Singleton.MinimumLevel = level;
        LSLogger.Singleton.SetSourceStatus((sourceID: ClassName, isEnabled: status));
        LSLogger.Singleton.SetSourceStatus((sourceID: LSProcess.ClassName, isEnabled: status));
        LSLogger.Singleton.SetSourceStatus((sourceID: LSProcessManager.ClassName, isEnabled: status));
        LSLogger.Singleton.SetSourceStatus((sourceID: LSProcessNodeHandler.ClassName, isEnabled: status));
        LSLogger.Singleton.SetSourceStatus((sourceID: LSProcessNodeSequence.ClassName, isEnabled: status));
        LSLogger.Singleton.SetSourceStatus((sourceID: LSProcessNodeSelector.ClassName, isEnabled: status));
        LSLogger.Singleton.SetSourceStatus((sourceID: LSProcessNodeParallel.ClassName, isEnabled: status));
        LSLogger.Singleton.SetSourceStatus((sourceID: LSProcessSession.ClassName, isEnabled: status));
        LSLogger.Singleton.SetSourceStatus((sourceID: LSProcessTreeBuilder.ClassName, isEnabled: status));
    }

    /// <summary>
    /// Two-level concurrent dictionary storing registered processing contexts.
    /// <para>
    /// Structure: ProcessType -> (ILSProcessable -> ILSProcessLayerNode)<br/>
    /// - Outer key: The concrete process type (e.g., typeof(MyCustomProcess))<br/>
    /// - Inner key: ILSProcessable instance or GlobalProcessable.Instance for global contexts<br/>
    /// - Value: The registered processing node hierarchy for that type+instance combination
    /// </para>
    /// <para>
    /// Thread-safe for concurrent registration and retrieval operations across multiple threads.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<System.Type, ConcurrentDictionary<ILSProcessable, ILSProcessLayerNode>> _globalNodes = new();

    public LSProcessManager() { }
    /// <summary>
    /// Registers a processing context for a specific process type using a fluent builder pattern.
    /// <para>
    /// Creates or extends the processing hierarchy for the given process type and instance.
    /// If the type+instance combination already exists, the builder extends the existing tree.
    /// </para>
    /// </summary>
    /// <typeparam name="TProcess">The process type to register the context for (must implement ILSProcess)</typeparam>
    /// <param name="builder">Builder action that defines the processing hierarchy to add</param>
    /// <param name="instance">Optional processable instance for instance-specific registration (null = global)</param>
    /// <param name="layerType">Root layer type for new registrations (PARALLEL, SEQUENCE, SELECTOR)</param>
    /// <example>
    /// Registering global and instance-specific contexts:
    /// <code>
    /// // Global context - applies to all MockProcess instances
    /// LSProcessManager.Singleton.Register&lt;MockProcess&gt;(root => root
    ///     .Sequence("global-logic", seq => seq
    ///         .Handler("validate", session => LSProcessResultStatus.SUCCESS)
    ///         .Handler("execute", session => LSProcessResultStatus.SUCCESS)
    ///     )
    /// );
    /// 
    /// // Instance-specific context - only for this particular entity
    /// var playerEntity = new PlayerEntity(Guid.NewGuid(), "Player");
    /// LSProcessManager.Singleton.Register&lt;MockProcess&gt;(root => root
    ///     .Handler("player-bonus-logic", session => LSProcessResultStatus.SUCCESS),
    ///     instance: playerEntity
    /// );
    /// </code>
    /// </example>
    public void Register<TProcess>(LSProcessBuilderAction builder, ILSProcessable? instance = null) where TProcess : LSProcess {
        Register(typeof(TProcess), builder, instance);
    }
    /// <summary>
    /// Core registration method that handles the actual context storage and tree building.
    /// <para>
    /// Implementation Details:<br/>
    /// 1. Thread-safe dictionary access with TryAdd fallback for new process types<br/>
    /// 2. Uses GlobalProcessable.Instance as key for global (instance-less) registrations<br/>
    /// 3. Creates new root nodes based on layerType for first-time registrations<br/>
    /// 4. Extends existing trees when type+instance combination already exists<br/>
    /// 5. Stores the built result back to the concurrent dictionary
    /// </para>
    /// </summary>
    /// <param name="processType">The concrete process type (e.g., typeof(MyCustomProcess))</param>
    /// <param name="builder">Builder action that defines the processing hierarchy to add</param>
    /// <param name="instance">Processable instance for targeted registration (null = global context)</param>
    /// <exception cref="LSException">Thrown if concurrent dictionary operations fail</exception>
    public void Register(System.Type processType, LSProcessBuilderAction builder, ILSProcessable? instance = null) {
        var processDict = _globalNodes.GetOrAdd(processType, _ => new ConcurrentDictionary<ILSProcessable, ILSProcessLayerNode>());

        LSLogger.Singleton.Debug($"{ClassName}.Register<{processType.Name}>: {(instance != null ? instance.ID.ToString() : "global")}",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        instance ??= GlobalProcessable.Instance;
        if (!processDict.TryGetValue(instance, out var instanceNode)) {
            // first time registration for this type+instance, create new root node
            instanceNode = CreateRootNode(processType.Name);
        }
        var instanceBuilder = new LSProcessTreeBuilder(instanceNode);
        // Build the root node using the provided builder action
        var root = builder(instanceBuilder).Build();
        processDict[instance] = root;

        LSLogger.Singleton.Debug("Manager Register Tree",
            source: (ClassName, null),
            properties: new (string, object)[] {
                (nameof(processType), processType.Name),
                (nameof(root.NodeID), root.NodeID),
                (nameof(instance), instance == GlobalProcessable.Instance ? "global" : instance.ID.ToString()),
                (nameof(Register), nameof(Register))
    });


    }
    /// <summary>
    /// This is the primary method used by LSProcess.Execute() to obtain the merged processing
    /// hierarchy before creating an execution session.
    /// <param name="processType">The concrete process type to resolve contexts for</param>
    /// <param name="instance">Optional processable instance for targeted context resolution</param>
    /// <param name="localNode">Optional local context tree to merge with highest priority</param>
    /// <param name="contextMode">Flags controlling which context levels to include in the merge</param>
    /// <returns>Fully merged root node ready for process execution</returns>
    /// <exception cref="LSException">Thrown if process type dictionary creation fails</exception>
    public ILSProcessLayerNode GetRootNode(System.Type processType, out ILSProcessable[]? availableInstances, bool firstMatch = false, params ILSProcessable[]? instanceNodes) {
        var processDict = _globalNodes.GetOrAdd(processType, _ => new ConcurrentDictionary<ILSProcessable, ILSProcessLayerNode>());

        // Create a new builder starting with the localNode as root node
        LSProcessTreeBuilder builder = new LSProcessTreeBuilder();

        availableInstances = System.Array.Empty<ILSProcessable>();
        if (instanceNodes == null || instanceNodes.Length == 0) {
            instanceNodes = new[] { GlobalProcessable.Instance };
        }
        if (firstMatch) {
            // use the first available instance context (try to match from first to last)
            foreach (var instance in instanceNodes) {
                if (processDict.TryGetValue(instance, out var instanceNode) == false) {
                    continue;
                }
                var clone = instanceNode.Clone();
                builder.Merge(clone);
                availableInstances = new[] { instance };
                break;
            }
        } else {
            // use all provided instance contexts that have registered handlers
            List<ILSProcessable> availableList = new();
            foreach (var instance in instanceNodes) {
                if (processDict.TryGetValue(instance, out var instanceNode)) {
                    var clone = instanceNode.Clone();
                    builder.Merge(clone);
                    availableList.Add(instance);
                }
            }
            availableInstances = availableList.ToArray();
        }

        try {
            // Build the final merged tree, if no nodes were merged, this will throw an exception
            return builder.Build();
        } catch (LSException) {
            // Graceful fallback: empty root using process type name
            return CreateRootNode(processType.Name);
        }
    }
    public static ILSProcessLayerNode CreateRootNode(string nodeID) {
        var sequence = new LSProcessNodeSequence(nodeID, 0, LSProcessPriority.NORMAL, NodeUpdatePolicy.DEFAULT_LAYER | NodeUpdatePolicy.IGNORE_CHANGES);
        return sequence;
    }
    [Flags]
    public enum LSProcessContextMode {
        /// <summary>
        /// Local context and built-in processing are always included
        /// </summary>
        LOCAL = 0,
        /// <summary>
        /// Global context registered without specific instance
        /// </summary>
        GLOBAL = 1 << 0,
        /// <summary>
        /// Use the first available provided instance context
        /// Case use: we have multiple provided instances, and we want to use the first one that has a registered context.
        /// </summary>
        MATCH_FIRST = 1 << 1,
        /// <summary>
        /// Use all provided instance contexts
        /// Case use: we have multiple provided instances, and we want to use all that have a registered context.
        /// The merged context will include all matched instance contexts.
        /// </summary>
        ALL_INSTANCES = 1 << 2,
        /// <summary>
        /// Use any available context: global + first available instance context
        /// </summary>
        ANY = LOCAL | GLOBAL | MATCH_FIRST,
        /// <summary>
        /// Use all available contexts: global + all provided instance contexts
        /// </summary>
        ALL = LOCAL | GLOBAL | ALL_INSTANCES,
    }

    /// <summary>
    /// Sentinel object used as dictionary key for global (instance-less) context registrations.
    /// <para>
    /// This class implements ILSProcessable to satisfy the dictionary key requirements
    /// while clearly indicating that the associated context applies globally rather than to
    /// a specific processable instance.
    /// </para>
    /// <para>
    /// <b>Design Pattern:</b><br/>
    /// Uses the Null Object pattern to provide a valid ILSProcessable key while indicating
    /// the absence of a specific instance. This allows the same dictionary structure to handle
    /// both global and instance-specific contexts without additional complexity.
    /// </para>
    /// </summary>
    public class GlobalProcessable : ILSProcessable {
        static GlobalProcessable _instance = new();
        public static GlobalProcessable Instance => _instance;

        /// <summary>
        /// Returns a new GUID each time to ensure this sentinel is never used for actual identity comparison.
        /// The changing ID prevents accidental reliance on this placeholder for real processable operations.
        /// </summary>
        public System.Guid ID => System.Guid.NewGuid();

        /// <summary>
        /// Throws NotImplementedException as this sentinel should never participate in actual processing.
        /// <para>
        /// This method exists only to satisfy the ILSProcessable interface contract.
        /// If called, it indicates a design error where the sentinel is being used as a real processable.
        /// </para>
        /// </summary>
        /// <param name="initBuilder">Ignored - not used by sentinel implementation</param>
        /// <param name="manager">Ignored - not used by sentinel implementation</param>
        /// <returns>Never returns - always throws exception</returns>
        /// <exception cref="NotImplementedException">Always thrown to prevent misuse of this sentinel object</exception>
        LSProcessResultStatus ILSProcessable.Initialize(LSProcessBuilderAction? initBuilder, LSProcessManager? manager, params ILSProcessable[]? forwardProcessables) {
            throw new System.NotImplementedException("GlobalProcessable is a placeholder class and should never be initialized in the processing pipeline.");
        }
    }
    public static string CreateNodeID<TNode>(ILSProcessNode? rootNode) where TNode : ILSProcessNode {
        return $"{rootNode?.NodeID ?? "root"}-{typeof(TNode).Name}-{System.Guid.NewGuid()}";
    }
}
