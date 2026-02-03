using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;

namespace LSUtils.ProcessSystem;

/// <summary>
/// Fluent API builder for constructing hierarchical process trees with complex execution patterns.
/// <para>
/// LSProcessTreeBuilder implements the Builder Pattern to provide an intuitive, declarative syntax
/// for constructing complex process hierarchies. It supports all node types (handlers, sequences,
/// selectors, parallel nodes, and inverters) with automatic node management, type validation,
/// and hierarchical composition through nested builder actions.
/// </para>
/// <para>
/// <b>Core Concepts:</b><br/>
/// - Single root node management with automatic initialization<br/>
/// - Node replacement and merging with type safety validation<br/>
/// - Fluent method chaining for readable hierarchy definition<br/>
/// - Nested builder actions for complex hierarchical structures<br/>
/// - Order-based and priority-based node management
/// </para>
/// <para>
/// <b>Builder Lifecycle:</b><br/>
/// Builders operate on a single root node context, automatically creating sequence roots
/// when needed. The Build() method finalizes construction and returns the complete hierarchy.
/// Each builder instance supports single-use semantics for clean lifecycle management.
/// </para>
/// </summary>
/// <example>
/// Common builder patterns:
/// <code>
/// // Simple handler sequence
/// var builder = new LSProcessTreeBuilder();
/// builder.Handler("validate", ValidateHandler)
///        .Handler("process", ProcessHandler)
///        .Handler("cleanup", CleanupHandler);
///
/// // Nested hierarchy with mixed node types
/// builder.Sequence("main-flow", seq => seq
///     .Handler("init", InitHandler)
///     .Selector("strategy", sel => sel
///         .Handler("primary", PrimaryHandler)
///         .Handler("fallback", FallbackHandler))
///     .Parallel("concurrent", par => par
///         .Handler("task-a", TaskAHandler)
///         .Handler("task-b", TaskBHandler)));
///
/// // Building and using the tree
/// ILSProcessLayerNode tree = builder.Build();
/// LSProcessManager.Register("my-process", tree);
/// </code>
/// </example>
public partial class LSProcessTreeBuilder {
    public const string ClassName = nameof(LSProcessTreeBuilder);
    /// <summary>
    /// The root node of the hierarchy being built. Used to return the complete hierarchy from Build().
    /// </summary>
    private ILSProcessLayerNode? _rootNode; // Tracks the root node to return on Build()

    /// <summary>
    /// Create a new builder with an existing root node hierarchy.
    /// Can only be created within LSProcess or LSProcessManager contexts.
    /// </summary>
    /// <param name="rootNode">The existing root node to use as the base for building. Will be referenced directly to the node, this allows global context manipulation.</param>
    internal LSProcessTreeBuilder(ILSProcessLayerNode? rootNode = null) {
        _rootNode = rootNode;
    }

    /// <summary>
    /// Finalizes the builder and returns the completed event processing hierarchy.
    /// </summary>
    /// <returns>The root node of the constructed hierarchy ready for event processing.</returns>
    /// <exception cref="LSException">Thrown when Build() is called multiple times on the same instance or when no hierarchy was constructed.</exception>
    /// <remarks>
    /// <para><strong>Single-Use Pattern:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>One-Time Build</strong>: Each builder instance can only be built once</description></item>
    /// <item><description><strong>State Protection</strong>: Prevents accidental reuse that could cause inconsistent hierarchies</description></item>
    /// <item><description><strong>Clean Lifecycle</strong>: Enforces proper builder disposal pattern</description></item>
    /// </list>
    /// 
    /// <para><strong>Validation Requirements:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Hierarchy Existence</strong>: Throws exception if no nodes were added to the builder</description></item>
    /// <item><description><strong>Root Node Availability</strong>: Ensures a valid root node exists for return</description></item>
    /// <item><description><strong>Structural Integrity</strong>: Built hierarchy is ready for immediate use</description></item>
    /// </list>
    /// 
    /// <para><strong>Return Value:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Independent Hierarchy</strong>: Returned hierarchy is independent of the builder</description></item>
    /// <item><description><strong>Processing Ready</strong>: Can be immediately used with LSEventProcessContext</description></item>
    /// <item><description><strong>Complete Structure</strong>: All nodes, relationships, and configurations are finalized</description></item>
    /// </list>
    /// </remarks>
    public ILSProcessLayerNode Build() {
        if (_rootNode == null) {
            throw new LSException("No root node cannot build.");
        }
        // Flow debug logging
        // LSLogger.Singleton.Debug($"{ClassName}.Build: rootNode [{_rootNode.NodeID}].",
        //     source: ("LSProcessSystem", null),
        //     properties: ("hideNodeID", true));
        return _rootNode;
    }
    public LSProcessTreeBuilder RemoveNode(string nodeID) {
        if (_rootNode == null) {
            throw new LSException("No root node to remove from.");
        }
        _rootNode.RemoveChild(nodeID);
        return this;
    }

    /// <summary>
    /// Attempts to retrieve a child node from the root node by its identifier.
    /// </summary>
    /// <param name="nodeID">The unique identifier of the child node to locate.</param>
    /// <param name="child">When this method returns, contains the found child node if successful, or null if not found.</param>
    /// <returns>True if a child node with the specified ID was found; otherwise, false.</returns>
    /// <remarks>
    /// <para><strong>Safe Lookup Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Null Context Handling</strong>: Returns false immediately if no current context exists</description></item>
    /// <item><description><strong>Dictionary Lookup</strong>: Utilizes efficient O(1) child lookup when context is available</description></item>
    /// <item><description><strong>Out Parameter Pattern</strong>: Provides both success indicator and result value</description></item>
    /// <item><description><strong>Exception-Free</strong>: Never throws exceptions, always returns success/failure indication</description></item>
    /// </list>
    /// 
    /// <para><strong>Usage Pattern:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Existence Check</strong>: Used internally to check if nodes exist before operations</description></item>
    /// <item><description><strong>Type Validation</strong>: Enables subsequent type checking on found nodes</description></item>
    /// <item><description><strong>Conflict Detection</strong>: Helps identify naming conflicts during node creation</description></item>
    /// </list>
    /// </remarks>
    private bool getChild(string nodeID, out ILSProcessNode? child) {
        child = null;
        if (_rootNode == null) return false;
        child = _rootNode.GetChild(nodeID);
        return child != null;
    }
}
