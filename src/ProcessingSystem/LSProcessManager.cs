namespace LSUtils.ProcessSystem;

using System.Collections.Concurrent;
using System.Collections.Generic;
using LSUtils.Logging;

/// <summary>
/// Manages global processing contexts for different process types and optional processable instances.
/// Provides centralized registration and retrieval of processing hierarchies within the LSProcessing system.
/// Only the manager should be able to register and provide contexts for the system.
/// </summary>
/// <remarks>
/// <para><strong>Manager Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Context Registration</strong>: Allows registration of processing hierarchies for specific process types</description></item>
/// <item><description><strong>Instance-Specific Contexts</strong>: Supports both global and instance-specific processing contexts</description></item>
/// <item><description><strong>Context Merging</strong>: Automatically merges global, instance-specific, and local contexts in priority order</description></item>
/// <item><description><strong>Singleton Access</strong>: Provides centralized access through singleton pattern for system-wide coordination</description></item>
/// </list>
/// 
/// <para><strong>Context Priority Order (highest to lowest):</strong></para>
/// <list type="number">
/// <item><description>Local contexts (passed directly to GetContext)</description></item>
/// <item><description>Instance-specific contexts (registered for specific ILSProcessable instances)</description></item>
/// <item><description>Global contexts (registered without specific instance)</description></item>
/// </list>
/// 
/// <para><strong>Usage Patterns:</strong></para>
/// <code>
/// // Register global context for all UserLoginProcess instances
/// LSProcessManager.Singleton.Register&lt;UserLoginProcess&gt;(builder => 
///     builder.Sequence("login-validation")
///            .Handler("validate-credentials", ValidateCredentials)
///            .Handler("update-last-login", UpdateLastLogin));
/// 
/// // Register instance-specific context for premium users
/// var premiumUser = new PremiumUser();
/// LSProcessManager.Singleton.Register&lt;UserLoginProcess&gt;(builder => 
///     builder.Handler("premium-benefits", ApplyPremiumBenefits), premiumUser);
/// 
/// // Retrieve merged context for processing
/// var context = LSProcessManager.Singleton.GetContext&lt;UserLoginProcess&gt;(premiumUser);
/// </code>
/// </remarks>
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
    /// Internal storage for registered processing contexts, organized by process type and processable instance.
    /// Maps process types to dictionaries containing instance-specific and global contexts.
    /// </summary>
    private readonly ConcurrentDictionary<System.Type, ConcurrentDictionary<ILSProcessable, ILSProcessLayerNode>> _globalNodes = new();

    public LSProcessManager() { }
    /// <summary>
    /// Registers a processing context for a specific process type using a fluent builder pattern.
    /// </summary>
    /// <typeparam name="TProcess">The process type to register the context for</typeparam>
    /// <param name="builder">Builder action that defines the processing hierarchy</param>
    /// <param name="instance">Optional processable instance for instance-specific registration</param>
    /// <remarks>
    /// If no instance is provided, registers a global context that applies to all instances of the process type.
    /// If an instance is provided, registers a context specific to that instance with higher priority than global contexts.
    /// </remarks>
    public void Register<TProcess>(LSProcessBuilderAction builder, ILSProcessable? instance = null) where TProcess : ILSProcess {
        Register(typeof(TProcess), builder, instance);
    }
    /// <summary>
    /// Registers a processing context for a specific process type using a fluent builder pattern.
    /// </summary>
    /// <param name="processType">The process type to register the context for</param>
    /// <param name="builder">Builder action that defines the processing hierarchy</param>
    /// <param name="instance">Optional processable instance for instance-specific registration</param>
    /// <exception cref="ArgumentNullException">Thrown when the builder delegate returns null</exception>
    /// <remarks>
    /// <para>The registration process follows these steps:</para>
    /// <list type="number">
    /// <item><description>Creates or retrieves the event dictionary for the process type</description></item>
    /// <item><description>Determines if this is a global or instance-specific registration</description></item>
    /// <item><description>Creates or extends existing context using LSProcessTreeBuilder</description></item>
    /// <item><description>Executes the builder action to define the processing hierarchy</description></item>
    /// <item><description>Stores the built context for future retrieval</description></item>
    /// </list>
    /// </remarks>
    public void Register(System.Type processType, LSProcessBuilderAction builder, ILSProcessable? instance = null) {
        if (!_globalNodes.TryGetValue(processType, out var processDict)) {
            processDict = new();
            if (!_globalNodes.TryAdd(processType, processDict)) throw new LSException("Failed to add new process type dictionary.");
        }
        LSLogger.Singleton.Debug($"{ClassName}.Register<{processType.Name}>: {(instance != null ? instance.ID.ToString() : "global")}",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        instance ??= GlobalProcessable.Instance;
        if (!processDict.TryGetValue(instance, out var instanceNode)) {
            instanceNode = new LSProcessTreeBuilder().Parallel($"{processType.Name}").Build();
        }
        var instanceBuilder = new LSProcessTreeBuilder(instanceNode);
        // Build the root node using the provided builder action
        var root = builder(instanceBuilder).Build();
        processDict[instance ?? GlobalProcessable.Instance] = root;

        LSLogger.Singleton.Debug("Manager Register Tree",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("processType", processType.Name),
                ("rootNodeID", root.NodeID),
                ("instance", instance?.ID.ToString() ?? "global"),
                ("method", nameof(Register))
    });


    }
    /// <summary>
    /// Retrieves a merged processing context for the specified process type and optional instance.
    /// </summary>
    /// <typeparam name="TProcess">The process type to retrieve the context for</typeparam>
    /// <param name="instance">Optional processable instance for instance-specific context merging</param>
    /// <param name="localContext">Optional local context to merge with highest priority</param>
    /// <returns>Merged processing hierarchy containing global, instance-specific, and local contexts</returns>
    /// <remarks>
    /// Contexts are merged in priority order: local context (highest) → instance-specific → global (lowest).
    /// Each context is cloned before merging to preserve the original registered contexts.
    /// </remarks>
    public ILSProcessLayerNode GetRootNode<TProcess>(ILSProcessable? instance = null, ILSProcessLayerNode? localContext = null) where TProcess : ILSProcess {
        return GetRootNode(typeof(TProcess), instance, localContext);
    }
    /// <summary>
    /// Retrieves a merged processing context for the specified process type and optional instance.
    /// </summary>
    /// <param name="processType">The process type to retrieve the context for</param>
    /// <param name="instance">Optional processable instance for instance-specific context merging</param>
    /// <param name="localNode">Optional local context to merge with highest priority</param>
    /// <returns>Merged processing hierarchy containing global, instance-specific, and local contexts</returns>
    /// <remarks>
    /// <para><strong>Context Merging Strategy:</strong></para>
    /// <list type="number">
    /// <item><description>Creates a root sequence node as the base hierarchy</description></item>
    /// <item><description>Merges global context (if registered) for the process type</description></item>
    /// <item><description>Merges instance-specific context (if registered and instance provided)</description></item>
    /// <item><description>Merges local context (if provided) with highest priority</description></item>
    /// </list>
    /// 
    /// <para>All contexts are cloned before merging to ensure the original registered contexts remain unmodified.</para>
    /// </remarks>
    public ILSProcessLayerNode GetRootNode(System.Type processType, ILSProcessable? instance = null, ILSProcessLayerNode? localNode = null) {
        var previousLoggerStatus = LSLogger.Singleton.GetSourceStatus(ClassName);
        //disable detailed logging during get node (avoid unnecessary logging), flow logging remains active
        LSLogger.Singleton.SetSourceStatus((sourceID: ClassName, isEnabled: false));
        // if processType is not registered, create a new process dictionary for this type.
        if (!_globalNodes.TryGetValue(processType, out var processDict)) {
            processDict = new();
            if (!_globalNodes.TryAdd(processType, processDict)) throw new LSException("Failed to add new process type dictionary.");
        }
        LSProcessTreeBuilder builder = new LSProcessTreeBuilder();
        if (processDict.TryGetValue(GlobalProcessable.Instance, out var globalNode)) {
            // we have a global node to merge. We clone the global node to avoid modifying the original.
            //builder.Merge(globalNode.Clone());
            builder.Merge(globalNode.Clone());
        }
        // merge instance specific node if available. We clone the instance node to avoid modifying the original.
        ILSProcessLayerNode? instanceNode = null;
        if (instance != null && processDict.TryGetValue(instance, out instanceNode)) {
            builder.Merge(instanceNode.Clone());
        }
        // local context is merged last, so it has priority over global and instance contexts.
        if (localNode != null) {
            builder.Merge(localNode);
        }
        var root = builder.Build();
        //restore detailed logger status
        LSLogger.Singleton.SetSourceStatus((sourceID: ClassName, isEnabled: previousLoggerStatus));
        //flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.GetRootNode<{processType.Name}>: [{root.NodeID}]",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));
        // debug log with details
        LSLogger.Singleton.Debug("Manager Get Root Tree",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("processType", processType.Name),
                ("rootNodeID", root.NodeID),
                ("global", globalNode?.GetType().Name ?? "n/a"), ("globalNodeID", globalNode?.NodeID ?? "null"),
                ("instance", instanceNode?.GetType().Name ?? "n/a"), ("instanceNodeID", instanceNode?.NodeID ?? "null"),
                ("local", localNode?.GetType().Name ?? "n/a"), ("localNodeID", localNode?.NodeID ?? "null"),
                ("method", nameof(GetRootNode))
            });
        return root;
    }
    /// <summary>
    /// Internal placeholder class representing global processable contexts.
    /// Used as a key for storing process contexts that apply globally rather than to specific instances.
    /// </summary>
    /// <remarks>
    /// This class serves as a sentinel value in the context dictionary to distinguish between
    /// global contexts (registered without a specific instance) and instance-specific contexts.
    /// It should never be used directly outside of the LSProcessManager implementation.
    /// </remarks>
    private class GlobalProcessable : ILSProcessable {
        static GlobalProcessable _instance = new();
        internal static GlobalProcessable Instance => _instance;
        /// <summary>
        /// Gets a new GUID for each access. This ensures the global processable is treated uniquely
        /// but should not be used for identity comparison in the processing system.
        /// </summary>
        public System.Guid ID => System.Guid.NewGuid();
        /// <summary>
        /// Not implemented as this placeholder class should never be initialized in the processing pipeline.
        /// </summary>
        /// <param name="ctxBuilder">Ignored parameter</param>
        /// <param name="manager">Ignored parameter</param>
        /// <returns>Never returns as method throws NotImplementedException</returns>
        /// <exception cref="NotImplementedException">Always thrown as this is a placeholder implementation</exception>
        LSProcessResultStatus ILSProcessable.Initialize(LSProcessBuilderAction? initBuilder, LSProcessManager? manager) {
            throw new System.NotImplementedException("GlobalProcessable is a placeholder class and should never be initialized in the processing pipeline.");
        }
    }
}
