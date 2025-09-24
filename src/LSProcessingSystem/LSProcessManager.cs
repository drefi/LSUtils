using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LSUtils.Processing;

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
    public static LSProcessManager Singleton { get; } = new LSProcessManager();
    /// <summary>
    /// Internal storage for registered processing contexts, organized by process type and processable instance.
    /// Maps process types to dictionaries containing instance-specific and global contexts.
    /// </summary>
    protected readonly Dictionary<System.Type, Dictionary<ILSProcessable, ILSProcessLayerNode>> _globalContext = new();
    
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
        if (!_globalContext.TryGetValue(processType, out var processDict)) {
            processDict = new Dictionary<ILSProcessable, ILSProcessLayerNode>();
            _globalContext[processType] = processDict;
        }
        LSProcessTreeBuilder contextBuilder;
        // if no instance is provided, we register a global context.
        if (instance == null) {
            if (!processDict.TryGetValue(GlobalProcessable.Instance, out var globalNode)) {
                // no global context registered for this process type, create a new parallel node using the processType as nodeID.
                contextBuilder = new LSProcessTreeBuilder().Parallel($"{processType.Name}");
            } else {
                contextBuilder = new LSProcessTreeBuilder(globalNode);
            }
        } else { // if an instance is provided, we register a context specific to that instance.
                 // check if we have a context already registered for this instance.            
            if (!processDict.TryGetValue(instance, out var instanceNode)) {
                instanceNode = new LSProcessTreeBuilder().Parallel($"{processType.Name}").Build();
            }
            contextBuilder = new LSProcessTreeBuilder(instanceNode);
        }
        var result = builder(contextBuilder);
        if (result == null) {
            throw new ArgumentNullException(nameof(builder), "The context builder delegate returned null.");
        }
        processDict[instance ?? GlobalProcessable.Instance] = result.Build();

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
    public ILSProcessLayerNode GetContext<TProcess>(ILSProcessable? instance = null, ILSProcessLayerNode? localContext = null) where TProcess : ILSProcess {
        return GetContext(typeof(TProcess), instance, localContext);
    }
    
    /// <summary>
    /// Retrieves a merged processing context for the specified process type and optional instance.
    /// </summary>
    /// <param name="processType">The process type to retrieve the context for</param>
    /// <param name="instance">Optional processable instance for instance-specific context merging</param>
    /// <param name="localContext">Optional local context to merge with highest priority</param>
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
    public ILSProcessLayerNode GetContext(System.Type processType, ILSProcessable? instance = null, ILSProcessLayerNode? localContext = null) {
        // if processType is not registered, create a new process dictionary for this type.
        if (!_globalContext.TryGetValue(processType, out var processDict)) {
            processDict = new Dictionary<ILSProcessable, ILSProcessLayerNode>();
            _globalContext[processType] = processDict;
        }
        LSProcessTreeBuilder builder = new LSProcessTreeBuilder().Sequence($"root");
        if (!processDict.TryGetValue(GlobalProcessable.Instance, out var globalNode)) {
            // no global context registered for this process type; create a new root parallel node.
            builder.Parallel($"{processType.Name}");
        } else {
            // we have a globalContext; start with a clone of the global node to avoid modifying the original.
            builder.Merge(globalNode.Clone());
        }
        // merge instance specific context if available. We always use the clone of the node, so we don't modify the original instance node.
        if (instance != null) {
            if (!processDict.TryGetValue(instance, out var instanceNode)) {
                builder.Parallel($"{processType.Name}");
            } else {
                builder.Merge(instanceNode.Clone());
            }
        }
        // local context is merged last, so it has priority over global and instance contexts.
        if (localContext != null) {
            builder.Merge(localContext);
        }

        return builder.Build();
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
    protected class GlobalProcessable : ILSProcessable {
        static GlobalProcessable _instance = new();
        internal static GlobalProcessable Instance => _instance;

        /// <summary>
        /// Gets a new GUID for each access. This ensures the global processable is treated uniquely
        /// but should not be used for identity comparison in the processing system.
        /// </summary>
        public System.Guid ID => Guid.NewGuid();

        /// <summary>
        /// Not implemented as this placeholder class should never be initialized in the processing pipeline.
        /// </summary>
        /// <param name="ctxBuilder">Ignored parameter</param>
        /// <param name="manager">Ignored parameter</param>
        /// <returns>Never returns as method throws NotImplementedException</returns>
        /// <exception cref="NotImplementedException">Always thrown as this is a placeholder implementation</exception>
        LSProcessResultStatus ILSProcessable.Initialize(LSProcessBuilderAction? ctxBuilder, LSProcessManager? manager) {
            throw new System.NotImplementedException("GlobalProcessable is a placeholder class and should never be initialized in the processing pipeline.");
        }
    }
}
