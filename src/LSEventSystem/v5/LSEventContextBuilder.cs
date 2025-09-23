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
    /// Tracks whether Build() has been called to prevent multiple builds from the same instance.
    /// </summary>
    /// <remarks>
    /// <para>Once Build() is called, this flag is set to true and subsequent Build() calls will throw an LSException.</para>
    /// <para>This ensures the builder follows the expected pattern of single-use instances.</para>
    /// </remarks>
    protected bool _hasBuilt = false;

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
    }

    /// <summary>
    /// Initializes a new builder in event mode with an existing root node hierarchy.
    /// </summary>
    /// <param name="rootNode">The existing root node to use as the base for building. Will be referenced directly to the node, this allows global context manipulation.</param>
    /// <remarks>
    /// <para><strong>Event Mode Construction:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Current Context</strong>: Sets both current and root to the cloned hierarchy</description></item>
    /// <item><description><strong>Extension Ready</strong>: Ready to add children or navigate into existing structure</description></item>
    /// </list>
    /// </remarks>
    internal LSEventContextBuilder(ILSEventLayerNode rootNode) {
        _currentNode = _rootNode = rootNode;
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
    /// <item><description><strong>Context</strong>: If <c>_currentNode</c> is null, a new root sequence node named: <c>sequence[{nodeID}]</c> is created</description></item>
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
    public LSEventContextBuilder Execute(string nodeID, LSEventHandler handler, LSPriority priority = LSPriority.NORMAL, bool withInverter = false, params LSEventCondition?[] conditions) {
        if (_currentNode == null) {
            _currentNode = LSEventSequenceNode.Create($"sequence[{nodeID}]", 0); // create a root sequence node if none exists
        }
        // Check if a node with the same ID already exists
        if (tryGetContextChild(nodeID, out var existingNode)) {
            // Node with same ID already exists
            // If the existing node is not a handler node, we cannot override it with a handler node
            if (existingNode is not LSEventHandlerNode) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a handler node.");
            // remove the previous handler node
            _currentNode.RemoveChild(nodeID);
        }
        int order = _currentNode.GetChildren().Length; // Order is based on the number of existing children
        LSEventHandlerNode handlerNode = LSEventHandlerNode.Create(nodeID, handler, order, priority, withInverter, conditions);
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
    
    /// <summary>
    /// Creates or navigates to a sequence node in the current context with support for nested hierarchy construction.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the sequence node within the current context.</param>
    /// <param name="sequenceContext">Optional delegate for building nested children within this sequence. If provided, the sequence is built completely and the builder stays at the parent level.</param>
    /// <param name="priority">Processing priority level for this sequence node (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met before this sequence processes.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSException">Thrown when attempting to override a non-sequence node with the same ID.</exception>
    /// <remarks>
    /// <para><strong>Sequence Node Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Sequential Processing</strong>: Children are processed in priority/order sequence until one fails</description></item>
    /// <item><description><strong>Early Termination</strong>: Processing stops at the first child that returns FAILURE</description></item>
    /// <item><description><strong>Success Condition</strong>: All children must succeed for the sequence to succeed</description></item>
    /// <item><description><strong>AND Logic</strong>: Implements logical AND behavior across child nodes</description></item>
    /// </list>
    /// 
    /// <para><strong>Node Creation and Replacement:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>New Node Creation</strong>: Creates a new sequence node if none exists with the given ID</description></item>
    /// <item><description><strong>Existing Node Navigation</strong>: Navigates to existing sequence node if it exists</description></item>
    /// <item><description><strong>Type Safety</strong>: Throws exception if attempting to replace non-sequence node</description></item>
    /// <item><description><strong>Order Preservation</strong>: Maintains original order when replacing existing nodes</description></item>
    /// </list>
    /// 
    /// <para><strong>Navigation Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Root Creation</strong>: If no root exists, this sequence becomes the root node</description></item>
    /// <item><description><strong>Sub-Context Building</strong>: When subContextBuilder is provided, stays at parent level after building</description></item>
    /// <item><description><strong>Direct Navigation</strong>: When no subContextBuilder provided, navigates into the sequence for further operations</description></item>
    /// <item><description><strong>Sibling Support</strong>: Enables creation of sibling nodes when using sub-context pattern</description></item>
    /// </list>
    /// </remarks>
    public LSEventContextBuilder Sequence(string nodeID,
            LSEventContextDelegate? sequenceContext = null,
            LSPriority priority = LSPriority.NORMAL,
            bool withInverter = false,
            params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventSequenceNode sequenceNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not sequence
            sequenceNode = LSEventSequenceNode.Create(nodeID, order, priority, withInverter, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventSequenceNode we should throw an exception.
            //if existingNode is not null here is means is not a sequence node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a sequence node.");
            _currentNode?.RemoveChild(nodeID); // remove existing node if exists
        }
        // when we reach this point, sequenceNode is either a new node or the existing sequence node
        ILSEventLayerNode node = sequenceContext?.Invoke(new LSEventContextBuilder(sequenceNode)).Build() ?? sequenceNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || sequenceContext == null) ? node : parentBefore;

        return this;
    }
    /// <summary>
    /// Creates or navigates to a selector node in the current context with support for nested hierarchy construction.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the selector node within the current context.</param>
    /// <param name="selectorBuilder">Optional delegate for building nested children within this selector. If provided, the selector is built completely and the builder stays at the parent level.</param>
    /// <param name="priority">Processing priority level for this selector node (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met before this selector processes.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSException">Thrown when attempting to override a non-selector node with the same ID.</exception>
    /// <remarks>
    /// <para><strong>Selector Node Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Sequential Processing</strong>: Children are processed in priority/order sequence until one succeeds</description></item>
    /// <item><description><strong>Early Termination</strong>: Processing stops at the first child that returns SUCCESS</description></item>
    /// <item><description><strong>Failure Condition</strong>: All children must fail for the selector to fail</description></item>
    /// <item><description><strong>OR Logic</strong>: Implements logical OR behavior across child nodes</description></item>
    /// </list>
    /// 
    /// <para><strong>Node Creation and Replacement:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>New Node Creation</strong>: Creates a new selector node if none exists with the given ID</description></item>
    /// <item><description><strong>Existing Node Navigation</strong>: Navigates to existing selector node if it exists</description></item>
    /// <item><description><strong>Type Safety</strong>: Throws exception if attempting to replace non-selector node</description></item>
    /// <item><description><strong>Order Preservation</strong>: Maintains original order when replacing existing nodes</description></item>
    /// </list>
    /// 
    /// <para><strong>Navigation Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Root Creation</strong>: If no root exists, this selector becomes the root node</description></item>
    /// <item><description><strong>Sub-Context Building</strong>: When subContextBuilder is provided, stays at parent level after building</description></item>
    /// <item><description><strong>Direct Navigation</strong>: When no subContextBuilder provided, navigates into the selector for further operations</description></item>
    /// <item><description><strong>Sibling Support</strong>: Enables creation of sibling nodes when using sub-context pattern</description></item>
    /// </list>
    /// </remarks>
    public LSEventContextBuilder Selector(string nodeID, LSEventContextDelegate? selectorBuilder = null, LSPriority priority = LSPriority.NORMAL, bool withInverter = false, params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventSelectorNode selectorNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not selector
            selectorNode = LSEventSelectorNode.Create(nodeID, order, priority, withInverter, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventSelectorNode we should throw an exception.
            //if existingNode is not null here is means is not a selector node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a selector node.");
            _currentNode?.RemoveChild(nodeID);
        }
        // when we reach this point, selectorNode is either a new node or the existing selector node
        ILSEventLayerNode node = selectorBuilder?.Invoke(new LSEventContextBuilder(selectorNode)).Build() ?? selectorNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || selectorBuilder == null) ? node : parentBefore;

        return this;
    }

    /// <summary>
    /// Creates or navigates to a parallel node in the current context with support for nested hierarchy construction.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the parallel node within the current context.</param>
    /// <param name="parallelBuilder">Optional delegate for building nested children within this parallel node. If provided, the parallel node is built completely and the builder stays at the parent level.</param>
    /// <param name="numRequiredToSucceed">The number of child nodes that must succeed for the parallel node to succeed (default: 0 - all must succeed).</param>
    /// <param name="numRequiredToFailure">The number of child nodes that must fail for the parallel node to fail (default: 0 - any failure causes parallel failure).</param>
    /// <param name="priority">Processing priority level for this parallel node (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met before this parallel node processes.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSException">Thrown when attempting to override a non-parallel node with the same ID.</exception>
    /// <remarks>
    /// <para><strong>Parallel Node Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Concurrent Processing</strong>: All eligible children are processed simultaneously</description></item>
    /// <item><description><strong>Threshold-Based Success</strong>: Success determined by numRequiredToSucceed threshold</description></item>
    /// <item><description><strong>Threshold-Based Failure</strong>: Failure determined by numRequiredToFailure threshold</description></item>
    /// <item><description><strong>Flexible Logic</strong>: Supports various parallel processing patterns through threshold configuration</description></item>
    /// </list>
    /// 
    /// <para><strong>Threshold Configuration:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Success Threshold</strong>: 0 means all children must succeed; positive value sets minimum success count</description></item>
    /// <item><description><strong>Failure Threshold</strong>: 0 means any failure causes parallel failure; positive value sets maximum failure tolerance</description></item>
    /// <item><description><strong>Dynamic Updates</strong>: Existing parallel nodes can have their success threshold updated</description></item>
    /// <item><description><strong>Validation</strong>: Thresholds are validated against actual child count during processing</description></item>
    /// </list>
    /// 
    /// <para><strong>Waiting and Resume Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Individual Resume</strong>: Parallel nodes in WAITING state can be resumed individually</description></item>
    /// <item><description><strong>Child State Aggregation</strong>: Overall parallel status depends on child states and thresholds</description></item>
    /// <item><description><strong>Partial Completion</strong>: Can succeed even if some children are still waiting</description></item>
    /// </list>
    /// 
    /// <para><strong>Node Creation and Replacement:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>New Node Creation</strong>: Creates a new parallel node if none exists with the given ID</description></item>
    /// <item><description><strong>Existing Node Updates</strong>: Updates success threshold of existing parallel nodes when specified</description></item>
    /// <item><description><strong>Type Safety</strong>: Throws exception if attempting to replace non-parallel node</description></item>
    /// <item><description><strong>Order Preservation</strong>: Maintains original order when replacing existing nodes</description></item>
    /// </list>
    /// </remarks>
    public LSEventContextBuilder Parallel(string nodeID, LSEventContextDelegate? parallelBuilder = null, int? numRequiredToSucceed = 0, int? numRequiredToFailure = 0, LSPriority priority = LSPriority.NORMAL, bool withInverter = false, params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventParallelNode parallelNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not parallel
            parallelNode = LSEventParallelNode.Create(nodeID, order, numRequiredToSucceed == null ? 0 : numRequiredToSucceed.Value, numRequiredToFailure == null ? 0 : numRequiredToFailure.Value, priority, withInverter, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventParallelNode we should throw an exception.
            //if existingNode is not null here is means is not a parallel node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a parallel node.");
            _currentNode?.RemoveChild(nodeID);
        } else {
            // node exists and is a parallel node, update numRequiredToSucceed if provided
            if (numRequiredToSucceed != null) parallelNode.NumRequiredToSucceed = numRequiredToSucceed.Value;
        }
        // when we reach this point, parallelNode is either a new node or the existing parallel node
        ILSEventLayerNode node = parallelBuilder?.Invoke(new LSEventContextBuilder(parallelNode)).Build() ?? parallelNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || parallelBuilder == null) ? node : parentBefore;

        return this;
    }
    /// <summary>
    /// Merges a sub-layer node hierarchy into the current context with intelligent conflict resolution.
    /// </summary>
    /// <param name="subLayer">The sub-layer node hierarchy to merge into the current context.</param>
    /// <param name="mergeBuilder">Optional delegate for building additional content within the merged hierarchy before integration.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSArgumentNullException">Thrown when subLayer is null.</exception>
    /// <exception cref="LSException">Thrown when no current context exists and subLayer is empty.</exception>
    /// <remarks>
    /// <para><strong>Merge Strategies:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Root Seeding</strong>: If no current context exists, non-empty subLayer becomes the root</description></item>
    /// <item><description><strong>Type-Based Merging</strong>: Same-type layer nodes are merged recursively</description></item>
    /// <item><description><strong>Node Replacement</strong>: Different-type nodes or handler nodes are replaced</description></item>
    /// <item><description><strong>Additive Integration</strong>: Non-conflicting nodes are added directly</description></item>
    /// </list>
    /// 
    /// <para><strong>Conflict Resolution Rules:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Same Type Layer Nodes</strong>: Merged recursively, preserving both hierarchies</description></item>
    /// <item><description><strong>Different Type Nodes</strong>: SubLayer node replaces existing node</description></item>
    /// <item><description><strong>Handler Node Conflicts</strong>: SubLayer handler replaces existing handler</description></item>
    /// <item><description><strong>Root Container Conflicts</strong>: SubLayer container replaces root if types differ</description></item>
    /// </list>
    /// 
    /// <para><strong>Entry-Level Merging:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Root Identity Check</strong>: Special handling when merging into root node with same ID</description></item>
    /// <item><description><strong>Root Type Compatibility</strong>: Same-type roots are content-merged, different types replace</description></item>
    /// <item><description><strong>Context Preservation</strong>: Current context and root references updated appropriately</description></item>
    /// </list>
    /// 
    /// <para><strong>Sub-Context Integration:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Pre-Build Processing</strong>: subContextBuilder can modify the subLayer before merging</description></item>
    /// <item><description><strong>Isolated Operations</strong>: subContextBuilder operates on cloned copy to prevent side effects</description></item>
    /// <item><description><strong>Seamless Integration</strong>: Built result is integrated using standard merge logic</description></item>
    /// </list>
    /// </remarks>
    public LSEventContextBuilder Merge(ILSEventLayerNode subLayer, LSEventContextDelegate? mergeBuilder = null) {
        if (subLayer == null) throw new LSArgumentNullException(nameof(subLayer), "Sub-layer node cannot be null.");
        // If there is no current context, allow seeding with a non-empty sub-layer
        if (_currentNode == null) {
            if (subLayer.GetChildren().Length == 0) throw new LSException("No current context to merge the sub-layer into. Make sure to start with a layer node (Sequence, Selector, Parallel).");
            var seeded = mergeBuilder?.Invoke(new LSEventContextBuilder(subLayer)).Build() ?? subLayer;
            _currentNode = seeded;
            _rootNode = seeded;
            return this;
        }
        ILSEventLayerNode node = mergeBuilder?.Invoke(new LSEventContextBuilder(subLayer)).Build() ?? subLayer;

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

    /// <summary>
    /// Recursively merges the contents of a source node hierarchy into a target node hierarchy.
    /// </summary>
    /// <param name="currentNode">The target layer node that will receive merged content.</param>
    /// <param name="subNode">The source layer node whose content will be merged into the target.</param>
    /// <remarks>
    /// <para><strong>Recursive Merge Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Deep Traversal</strong>: Recursively processes all children in the source hierarchy</description></item>
    /// <item><description><strong>Conflict Detection</strong>: Identifies existing children with same IDs in target hierarchy</description></item>
    /// <item><description><strong>Type-Aware Merging</strong>: Different merge strategies based on node types</description></item>
    /// <item><description><strong>Preservation Logic</strong>: Maintains existing structure while integrating new content</description></item>
    /// </list>
    /// 
    /// <para><strong>Merge Resolution Strategy:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Both Layer Nodes</strong>: Recursive merge to combine both hierarchies</description></item>
    /// <item><description><strong>Same Type Non-Layer</strong>: Source node replaces target node (e.g., handler replacement)</description></item>
    /// <item><description><strong>Different Types</strong>: Source node replaces target node regardless of type</description></item>
    /// <item><description><strong>No Conflict</strong>: Source node added directly to target</description></item>
    /// </list>
    /// 
    /// <para><strong>Node Reference Handling:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Direct Addition</strong>: Non-conflicting nodes are added without cloning</description></item>
    /// <item><description><strong>Builder Assumption</strong>: Assumes all nodes are already properly cloned or new</description></item>
    /// <item><description><strong>Reference Integrity</strong>: Maintains proper parent-child relationships</description></item>
    /// </list>
    /// </remarks>
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
    public ILSEventLayerNode Build() {
        if (_hasBuilt) throw new LSException("Build can only be called once per builder instance.");
        _hasBuilt = true;
        if (_rootNode != null) return _rootNode;
        // If nothing was built, throw to match expected behavior
        throw new LSException("No current node to build. Make sure to end all layers.");
    }

    /// <summary>
    /// Attempts to retrieve a child node from the current context by its identifier.
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
    private bool tryGetContextChild(string nodeID, out ILSEventNode? child) {
        child = null;
        if (_currentNode == null) return false;
        child = _currentNode.GetChild(nodeID);
        return child != null;
    }
}
