using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Provides a fluent API for building event processing hierarchies using the builder pattern.
/// </summary>
/// <remarks>
/// <para><strong>Builder Pattern Implementation:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Fluent Interface</strong>: Method chaining enables intuitive hierarchy construction</description></item>
/// <item><description><strong>Context Management</strong>: Tracks current position in the hierarchy for nested operations</description></item>
/// <item><description><strong>Safe Navigation</strong>: Prevents invalid operations through state validation</description></item>
/// <item><description><strong>Immutable Product</strong>: Build() produces independent hierarchy copies</description></item>
/// </list>
/// 
/// <para><strong>Dual Construction Modes:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Global Mode</strong>: Starts with no root context, first layer node becomes root</description></item>
/// <item><description><strong>Event Mode</strong>: Initialized with existing root node, operates on cloned copy</description></item>
/// <item><description><strong>Mode Detection</strong>: Automatically determined by constructor parameters</description></item>
/// <item><description><strong>Context Isolation</strong>: Event mode ensures original hierarchy remains unchanged</description></item>
/// </list>
/// 
/// <para><strong>Hierarchy Construction Features:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Layer Nodes</strong>: Supports creation of Sequence, Selector, and Parallel nodes</description></item>
/// <item><description><strong>Handler Nodes</strong>: Execute() method adds leaf handler nodes with associated delegates</description></item>
/// <item><description><strong>Sub-Context Navigation</strong>: Enter() and End() methods for nested hierarchy construction</description></item>
/// <item><description><strong>Node Replacement</strong>: Automatic replacement of existing nodes with same ID</description></item>
/// </list>
/// 
/// <para><strong>Fluent API Design:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Method Chaining</strong>: All methods return LSEventContextBuilder for continuous fluent operation</description></item>
/// <item><description><strong>Contextual Operations</strong>: Operations apply to current node in the hierarchy navigation</description></item>
/// <item><description><strong>Validation</strong>: Runtime checks prevent invalid operations like adding handlers without layer context</description></item>
/// <item><description><strong>Order Management</strong>: Automatic order assignment based on child addition sequence</description></item>
/// </list>
/// 
/// <para><strong>Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Dynamic Hierarchy Creation</strong>: Runtime construction of event processing trees</description></item>
/// <item><description><strong>Configuration-Based Setup</strong>: Building hierarchies from external configuration data</description></item>
/// <item><description><strong>Template Expansion</strong>: Creating variations of base hierarchies for different events</description></item>
/// <item><description><strong>Testing Support</strong>: Simplified hierarchy creation for unit tests</description></item>
/// </list>
/// </remarks>
public class LSEventContextBuilder {
    /// <summary>
    /// The current node context for hierarchy operations. Null when in global mode before first layer node creation.
    /// </summary>
    private ILSEventLayerNode? _currentNode;
    
    /// <summary>
    /// The root node of the hierarchy being built. Used to return the complete hierarchy from Build().
    /// </summary>
    private ILSEventLayerNode? _rootNode; // Tracks the root node to return on Build()

    /// <summary>
    /// Initializes a new builder in global mode with no existing hierarchy.
    /// </summary>
    /// <remarks>
    /// <para><strong>Global Mode Construction:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>No Root Context</strong>: Starts with null current and root nodes</description></item>
    /// <item><description><strong>First Layer Node</strong>: The first Sequence/Selector/Parallel call establishes the root</description></item>
    /// <item><description><strong>Clean Slate</strong>: Provides completely fresh hierarchy construction</description></item>
    /// </list>
    /// </remarks>
    internal LSEventContextBuilder() {
        //when creating a "global" context we start with no root context
    }

    /// <summary>
    /// Initializes a new builder in event mode with an existing root node hierarchy.
    /// </summary>
    /// <param name="rootNode">The existing root node to use as the base for building. Will be cloned to preserve original.</param>
    /// <remarks>
    /// <para><strong>Event Mode Construction:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Cloned Base</strong>: Creates independent copy of provided root node hierarchy</description></item>
    /// <item><description><strong>Current Context</strong>: Sets both current and root to the cloned hierarchy</description></item>
    /// <item><description><strong>Isolation</strong>: Original hierarchy remains unchanged during building operations</description></item>
    /// <item><description><strong>Extension Ready</strong>: Ready to add children or navigate into existing structure</description></item>
    /// </list>
    /// </remarks>
    internal LSEventContextBuilder(ILSEventLayerNode rootNode) {
        _currentNode = _rootNode = rootNode.Clone();
    }

    /// <summary>
    /// Creates a handler node in the current layer context for executing event processing logic.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the handler node within the current layer.</param>
    /// <param name="handler">The event handler delegate to execute when this node processes.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSException">Thrown when no layer context exists or attempting to override non-handler node.</exception>
    /// <remarks>
    /// <para><strong>Handler Node Creation:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Context Requirement</strong>: Must have active layer node (Sequence/Selector/Parallel) context</description></item>
    /// <item><description><strong>Automatic Ordering</strong>: Order assigned based on current number of children in the layer</description></item>
    /// <item><description><strong>Node Replacement</strong>: Existing handler nodes with same ID are automatically replaced</description></item>
    /// <item><description><strong>Type Safety</strong>: Cannot override non-handler nodes to maintain hierarchy integrity</description></item>
    /// </list>
    /// 
    /// <para><strong>Fluent API Integration:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Method Chaining</strong>: Returns builder instance for continued fluent operations</description></item>
    /// <item><description><strong>Context Preservation</strong>: Current layer context remains unchanged after handler addition</description></item>
    /// <item><description><strong>Validation</strong>: Immediate validation prevents invalid hierarchy construction</description></item>
    /// </list>
    /// 
    /// <para><strong>Handler Configuration:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Delegate Association</strong>: Links specific event handler delegate to the node</description></item>
    /// <item><description><strong>Priority Assignment</strong>: Controls execution order within parent layer node</description></item>
    /// <item><description><strong>Condition Attachment</strong>: Optional conditions control when handler executes</description></item>
    /// </list>
    /// </remarks>
    public LSEventContextBuilder Execute(string nodeID, LSEventHandler handler, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        if (_currentNode == null) throw new LSException("No current context to add the handler. Make sure to start with a layer node (Sequence, Selector, Parallel).");
        // Check if a node with the same ID already exists
        if (tryGetContextChild(nodeID, out var existingNode)) {
            // Node with same ID already exists
            // If the existing node is not a handler node, we cannot override it with a handler node
            if (existingNode is not LSEventHandlerNode) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a handler node.");
            // remove the previous handler node
            _currentNode.RemoveChild(nodeID);
        }
        int order = _currentNode.GetChildren().Length; // Order is based on the number of existing children
        LSEventHandlerNode handlerNode = LSEventHandlerNode.Create(nodeID, handler, order, priority, conditions);
        _currentNode.AddChild(handlerNode);

        return this;
    }

    /// <summary>
    /// Removes a child node from the current layer context by its identifier.
    /// </summary>
    /// <param name="nodeID">The identifier of the child node to remove.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSException">Thrown when no layer context exists for the removal operation.</exception>
    /// <remarks>
    /// <para><strong>Safe Removal Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>No-Op for Missing Nodes</strong>: Attempting to remove non-existent nodes does not throw exceptions</description></item>
    /// <item><description><strong>Context Requirement</strong>: Must have active layer node context to perform removal</description></item>
    /// <item><description><strong>Fluent Continuation</strong>: Returns builder for continued method chaining</description></item>
    /// </list>
    /// </remarks>
    public LSEventContextBuilder RemoveChild(string nodeID) {
        if (_currentNode == null) throw new LSException("No current context to remove the node from. Make sure to start with a layer node (Sequence, Selector, Parallel).");
        // Removing a non-existent node should be a no-op (no exception)
        _currentNode.RemoveChild(nodeID);

        return this;
    }
    public delegate LSEventContextBuilder SubContextBuilder(LSEventContextBuilder subBuilder);
    /// <summary>
    /// Create or navigate to a sequence node in the current context.
    /// When creating a new sequence node, if a node with the same ID already exists it will be replaced.
    /// </summary>
    /// <param name="nodeID">The ID of the sequence node.</param>
    /// <param name="priority">The priority of the sequence node.</param>
    /// <param name="conditions">The conditions for the sequence node process.</param>
    /// <returns> The current instance of the context builder.</returns>
    /// <exception cref="LSException"></exception>
    public LSEventContextBuilder Sequence(string nodeID, SubContextBuilder? subContextBuilder = null, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventSequenceNode sequenceNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not sequence
            sequenceNode = LSEventSequenceNode.Create(nodeID, order, priority, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventSequenceNode we should throw an exception.
            //if existingNode is not null here is means is not a sequence node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a sequence node.");
            _currentNode?.RemoveChild(nodeID); // remove existing node if exists
        }
        // when we reach this point, sequenceNode is either a new node or the existing sequence node
        ILSEventLayerNode node = subContextBuilder?.Invoke(new LSEventContextBuilder(sequenceNode)).Build() ?? sequenceNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || subContextBuilder == null) ? node : parentBefore;

        return this;
    }
    /// <summary>
    /// Create or navigate to a selector node in the current context.
    /// When creating a new selector node, if a node with the same ID already exists it will be replaced.
    /// </summary>
    /// <param name="nodeID">The ID of the selector node.</param>
    /// <param name="priority">The priority of the selector node.</param>
    /// <param name="conditions">The conditions for the selector node process.</param>
    /// <returns> The current instance of the context builder.</returns>
    /// <exception cref="LSException"></exception>
    public LSEventContextBuilder Selector(string nodeID, SubContextBuilder? subContextBuilder = null, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventSelectorNode selectorNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not selector
            selectorNode = LSEventSelectorNode.Create(nodeID, order, priority, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventSelectorNode we should throw an exception.
            //if existingNode is not null here is means is not a selector node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a selector node.");
            _currentNode?.RemoveChild(nodeID);
        }
        // when we reach this point, selectorNode is either a new node or the existing selector node
        ILSEventLayerNode node = subContextBuilder?.Invoke(new LSEventContextBuilder(selectorNode)).Build() ?? selectorNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || subContextBuilder == null) ? node : parentBefore;

        return this;
    }

    /// <summary>
    /// Create or navigate to a parallel node in the current context.
    /// Parallel nodes have some quirks in terms of waiting behaviour:
    /// 1. If a parallel node is waiting, it can be resumed individually.
    /// 2. The overall success or failure of the parallel node depends on its children.
    /// 3. 
    /// </summary>
    /// <param name="nodeID">The ID of the parallel node.</param>
    /// <param name="numRequiredToSucceed">The number of child nodes that must succeed.</param>
    /// <param name="priority">The priority of the parallel node.</param>
    /// <param name="conditions">The conditions for the parallel node process.</param>
    /// <returns> The current instance of the context builder.</returns>
    /// <exception cref="LSException"></exception>
    public LSEventContextBuilder Parallel(string nodeID, SubContextBuilder? subContextBuilder = null, int? numRequiredToSucceed = 0, int? numRequiredToFailure = 0, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventParallelNode parallelNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not parallel
            parallelNode = LSEventParallelNode.Create(nodeID, order, numRequiredToSucceed == null ? 0 : numRequiredToSucceed.Value, numRequiredToFailure == null ? 0 : numRequiredToFailure.Value, priority, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventParallelNode we should throw an exception.
            //if existingNode is not null here is means is not a parallel node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a parallel node.");
            _currentNode?.RemoveChild(nodeID);
        } else {
            // node exists and is a parallel node, update numRequiredToSucceed if provided
            if (numRequiredToSucceed != null) parallelNode.NumRequiredToSucceed = numRequiredToSucceed.Value;
        }
        // when we reach this point, parallelNode is either a new node or the existing parallel node
        ILSEventLayerNode node = subContextBuilder?.Invoke(new LSEventContextBuilder(parallelNode)).Build() ?? parallelNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || subContextBuilder == null) ? node : parentBefore;

        return this;
    }
    /// <summary>
    /// Merges a sub-layer node into the current context.
    /// </summary>
    /// <param name="subLayer">The sub-layer node to merge.</param>
    /// <param name="overrideNode">Whether to override an existing node.</param>
    /// <returns>The current instance of the context builder.</returns>
    /// <exception cref="LSArgumentNullException">Thrown when subLayer is null.</exception>
    public LSEventContextBuilder Merge(ILSEventLayerNode subLayer, SubContextBuilder? subContextBuilder = null) {
        if (subLayer == null) throw new LSArgumentNullException(nameof(subLayer), "Sub-layer node cannot be null.");
        // If there is no current context, allow seeding with a non-empty sub-layer
        if (_currentNode == null) {
            if (subLayer.GetChildren().Length == 0) throw new LSException("No current context to merge the sub-layer into. Make sure to start with a layer node (Sequence, Selector, Parallel).");
            var seeded = subContextBuilder?.Invoke(new LSEventContextBuilder(subLayer)).Build() ?? subLayer;
            _currentNode = seeded;
            _rootNode = seeded;
            return this;
        }
        ILSEventLayerNode node = subContextBuilder?.Invoke(new LSEventContextBuilder(subLayer)).Build() ?? subLayer;

        // Entry merge: check if merging into root itself
        if (_currentNode.NodeID == node.NodeID) {
            if (_currentNode.GetType() == node.GetType()) {
                // same type: merge contents
                mergeRecursive((ILSEventLayerNode)_currentNode, node);
            } else {
                // different types: replace root container
                _currentNode = node;
                _rootNode = node;
            }
            return this;
        }

        var existing = _currentNode.GetChild(node.NodeID);
        if (existing == null) {
            _currentNode.AddChild(node);
            return this;
        }

        if (existing is ILSEventLayerNode existingLayer && node is ILSEventLayerNode subLayerNode) {
            // If container types differ, replace; otherwise merge recursively
            if (existing.GetType() != node.GetType()) {
                _currentNode.RemoveChild(node.NodeID);
                _currentNode.AddChild(node);
            } else {
                mergeRecursive(existingLayer, subLayerNode);
            }
        } else {
            // Different types or non-layer nodes: replace
            _currentNode.RemoveChild(node.NodeID);
            _currentNode.AddChild(node);
        }
        return this; // Return the builder for method chaining
    }

    private void mergeRecursive(ILSEventLayerNode currentNode, ILSEventLayerNode subNode) {
        // Iterate through all children of the source node
        foreach (var subNodeChild in subNode.GetChildren()) {
            var existingChild = currentNode.GetChild(subNodeChild.NodeID);

            if (existingChild != null) {
                // Node exists, check if both are layer nodes for recursive merging
                if (existingChild is ILSEventLayerNode existingLayer && subNodeChild is ILSEventLayerNode subNodeLayer) {
                    // Both are layer nodes - merge recursively
                    mergeRecursive(existingLayer, subNodeLayer);
                } else if (existingChild.GetType() == subNodeChild.GetType()) {
                    // Same type but not layer nodes (e.g., both handlers) - replace with subNodeChild
                    currentNode.RemoveChild(subNodeChild.NodeID);
                    currentNode.AddChild(subNodeChild);
                } else {
                    // Different types - replace with subNodeChild
                    currentNode.RemoveChild(subNodeChild.NodeID);
                    currentNode.AddChild(subNodeChild);
                }
            } else {
                // Node doesn't exist
                // Add the entire subNodeChild directly
                // I don't think this should be cloned, when builder is used it should assume that all nodes are already either clones of global nodes or new nodes
                currentNode.AddChild(subNodeChild);
            }
        }
    }
    protected bool _hasBuilt = false;
    public ILSEventLayerNode Build() {
        if (_hasBuilt) throw new LSException("Build can only be called once per builder instance.");
        _hasBuilt = true;
        if (_rootNode != null) return _rootNode;
        // If nothing was built, throw to match expected behavior
        throw new LSException("No current node to build. Make sure to end all layers.");
    }

    private bool tryGetContextChild(string nodeID, out ILSEventNode? child) {
        child = null;
        if (_currentNode == null) return false;
        child = _currentNode.GetChild(nodeID);
        return child != null;
    }


    // findNodeRecursive probably will not be used anymore, but leaving it here for reference
    // private ILSEventNode? findNodeRecursive(ILSEventNode? node, string targetNodeID) {
    //     if (node == null) return null;
    //     if (node.NodeID == targetNodeID) return node;

    //     if (node is ILSEventLayerNode layerNode) {
    //         var found = layerNode.GetChild(targetNodeID);
    //         if (found != null) return found;

    //         // Deep search in children
    //         foreach (var child in layerNode.GetChildren()) {
    //             var deepFound = findNodeRecursive(child, targetNodeID);
    //             if (deepFound != null) return deepFound;
    //         }
    //     }

    //     return null;
    // }
}
