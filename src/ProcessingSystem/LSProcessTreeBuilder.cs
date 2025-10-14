using System.Collections.Generic;
using LSUtils.Logging;

namespace LSUtils.ProcessSystem;

/// <summary>
/// Provides a fluent API for building event processing hierarchies using the builder pattern.
/// </summary>
public class LSProcessTreeBuilder {
    public const string ClassName = nameof(LSProcessTreeBuilder);
    /// <summary>
    /// The current node context for hierarchy operations. Null when in global mode before first layer node creation.
    /// </summary>
    //private ILSProcessLayerNode? _currentNode;

    /// <summary>
    /// The root node of the hierarchy being built. Used to return the complete hierarchy from Build().
    /// </summary>
    private ILSProcessLayerNode? _rootNode; // Tracks the root node to return on Build()

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
    internal LSProcessTreeBuilder() {
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
    internal LSProcessTreeBuilder(ILSProcessLayerNode rootNode) {
        _rootNode = rootNode;
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
    public LSProcessTreeBuilder Handler(string nodeID, LSProcessHandler handler, LSProcessPriority priority = LSProcessPriority.NORMAL, bool readOnly = false, params LSProcessNodeCondition?[] conditions) {
        if (_rootNode == null) {
            // NOTE: in theory this should not be needed because register and WithProcessing should create the root node
            LSLogger.Singleton.Warning($"{ClassName}.Handler created root sequence node.",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("order", "n/a"),
                    ("priority", priority.ToString()),
                    ("conditions", conditions.Length.ToString()),
                    ("method", nameof(Handler))
                });
            _rootNode = LSProcessNodeSequence.Create($"sequence[{nodeID}]", 0);
        }
        int order = _rootNode.GetChildren().Length; // Order is based on the number of existing children
        var actionDebug = "create";

        // Check if a node with the same ID already exists
        if (getChild(nodeID, out var existingNode)) {
            // Node with same ID already exists
            if (existingNode is not LSProcessNodeHandler) {
                // does not make sense to replace a node that is not a handler                
                LSLogger.Singleton.Warning($"Node [{nodeID}] already exists, but is not a NodeHandler.",
                    source: (ClassName, true),
                    properties: new (string, object)[] {
                        ("nodeID", nodeID),
                        ("rootNode", _rootNode?.NodeID ?? "n/a"),
                        ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                        ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                        ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                        ("order", order.ToString()),
                        ("priority", priority.ToString()),
                        ("conditions", conditions.Length.ToString()),
                        ("method", nameof(Handler))
                    });
                return this;
            }
            if (existingNode.ReadOnly) {
                // does not make sense to replace a read-only node
                LSLogger.Singleton.Warning($"Node [{nodeID}] already exists and is read-only, cannot replace.",
                    source: (ClassName, true),
                    properties: new (string, object)[] {
                        ("nodeID", nodeID),
                        ("rootNode", _rootNode?.NodeID ?? "n/a"),
                        ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                        ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                        ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                        ("order", order.ToString()),
                        ("priority", priority.ToString()),
                        ("conditions", conditions.Length.ToString()),
                        ("method", nameof(Handler))
                    });
                return this;
            }
            // remove the previous handler node
            _rootNode.RemoveChild(nodeID);
            actionDebug = "replace";
        }
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Handler: {actionDebug} [{nodeID}]",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));

        LSProcessNodeHandler handlerNode = LSProcessNodeHandler.Create(nodeID, handler, order, priority, readOnly, conditions);
        _rootNode.AddChild(handlerNode);

        // Detailed debug logging
        LSLogger.Singleton.Debug($"Handler Node {actionDebug} [{nodeID}]",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("action", actionDebug),
                ("rootNode", _rootNode?.NodeID ?? "null"),
                ("order", order.ToString()),
                ("priority", priority.ToString()),
                ("conditions", conditions.Length.ToString()),
                ("method", nameof(Handler))
            });

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
    public LSProcessTreeBuilder RemoveChild(string nodeID) {
        if (_rootNode == null) {
            LSLogger.Singleton.Warning($"Cannot remove child node [{nodeID}] because root node does not exist.",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", "n/a"),
                    ("method", nameof(RemoveChild))
                });
            return this;
        }
        var found = _rootNode.GetChild(nodeID);
        LSLogger.Singleton.Debug($"{ClassName}.RemoveChild [{nodeID}] [{(found != null ? "exists" : "n/a")}]",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));

        if (found == null) {
            //log warning
            LSLogger.Singleton.Warning($"No child with ID '{nodeID}' exists.",
                source: (ClassName, null),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("method", nameof(RemoveChild))
                });
            return this;
        }
        _rootNode.RemoveChild(nodeID);

        // Detailed debug logging
        LSLogger.Singleton.Debug($"Removed Child [{nodeID}] from [{_rootNode.NodeID}].",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("rootNode", _rootNode.NodeID),
                ("method", nameof(RemoveChild))
            });

        return this;
    }

    /// <summary>
    /// Creates or navigates to a sequence node in the current context with support for nested hierarchy construction.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the sequence node within the current context.</param>
    /// <param name="sequenceBuilderAction">Optional delegate for building nested children within this sequence. If provided, the sequence is built completely and the builder stays at the parent level.</param>
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
    public LSProcessTreeBuilder Sequence(string nodeID,
            LSProcessBuilderAction? sequenceBuilderAction = null,
            LSProcessPriority? priority = LSProcessPriority.NORMAL,
            bool overrideConditions = false,
            bool readOnly = false,
            params LSProcessNodeCondition?[] conditions) {

        var childExists = getChild(nodeID, out ILSProcessNode? existingNode);
        // Flow debug logging
        var actionDebug = childExists ? "update" : "create";
        LSLogger.Singleton.Debug($"{ClassName}.Sequence: {actionDebug} [{nodeID}]",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));
        int order = _rootNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root

        if (existingNode is LSProcessNodeSequence sequenceNode) {
            //update only if the node is not read-only
            if (!existingNode.ReadOnly) {
                // if fields are not provided keep the existing values
                sequenceNode.Priority = priority ?? sequenceNode.Priority;
                sequenceNode.Conditions = LSProcessHelpers.UpdateConditions(overrideConditions, sequenceNode.Conditions, conditions);
            }
            //continue to build children if builder provided even if the node is read-only
        } else if (childExists) {
            // it does not make sense to modify a sequence with a selector or parallel children
            LSLogger.Singleton.Warning($"Node [{nodeID}] exists but is not a sequence node, builder not processed.",
                source: (ClassName, null),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                    ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                    ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                    ("order", order),
                    ("priority", priority?.ToString() ?? "n/a"),
                    ("overrideConditions", overrideConditions),
                    ("conditions", conditions != null ? conditions.Length.ToString() : "n/a"),
                    ("method", nameof(Sequence))
                });
            return this;
        } else {
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not sequence
            priority ??= LSProcessPriority.NORMAL; // default priority
            sequenceNode = LSProcessNodeSequence.Create(nodeID, order, priority.Value, readOnly, conditions);
            if (_rootNode == null) _rootNode = sequenceNode;
            else _rootNode.AddChild(sequenceNode);
        }
        // since we are passing a reference to the node, the builder will modify the node directly
        sequenceBuilderAction?.Invoke(new LSProcessTreeBuilder(sequenceNode));

        LSLogger.Singleton.Debug($"Sequence Node {actionDebug} [{nodeID}]",
            source: (ClassName, null),
            processId: null,
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("builderProvided", sequenceBuilderAction != null),
                ("rootNode", _rootNode?.NodeID ?? "null"),
                ("order", sequenceNode.Order.ToString()),
                ("priority", sequenceNode.Priority.ToString()),
                ("readOnly", sequenceNode.ReadOnly.ToString()),
                ("conditions", conditions.Length.ToString()),
                ("overrideConditions", overrideConditions.ToString()),
                ("method", nameof(Sequence))
            });

        return this;
    }
    /// <summary>
    /// Creates or navigates to a selector node in the current context with support for nested hierarchy construction.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the selector node within the current context.</param>
    /// <param name="selectorBuilderAction">Optional delegate for building nested children within this selector. If provided, the selector is built completely and the builder stays at the parent level.</param>
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
    public LSProcessTreeBuilder Selector(string nodeID,
            LSProcessBuilderAction? selectorBuilderAction = null,
            LSProcessPriority? priority = LSProcessPriority.NORMAL,
            bool overrideConditions = false,
            bool readOnly = false,
            params LSProcessNodeCondition?[] conditions) {

        var childFound = getChild(nodeID, out ILSProcessNode? existingNode);
        // Flow debug logging
        var actionDebug = childFound ? "update" : "create";
        LSLogger.Singleton.Debug($"{ClassName}.Selector: {actionDebug} [{nodeID}]",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));
        int order = _rootNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root

        if (existingNode is LSProcessNodeSelector selectorNode) {
            // update only if the node is not read-only
            if (!existingNode.ReadOnly) {
                // if fields are not provided keep the existing values
                selectorNode.Priority = priority ?? selectorNode.Priority;
                selectorNode.Conditions = LSProcessHelpers.UpdateConditions(overrideConditions, selectorNode.Conditions, conditions);
            }
            //continue to build children if builder provided even if the node is read-only
        } else if (childFound) {
            // it does not make sense to modify a selector child when the existing node is a sequence, parallel or handler.
            LSLogger.Singleton.Warning($"Node [{nodeID}] exists but is not a selector node, builder not processed.",
                source: (ClassName, null),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                    ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                    ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                    ("order", order),
                    ("priority", priority?.ToString() ?? "n/a"),
                    ("overrideConditions", overrideConditions),
                    ("conditions", conditions != null ? conditions.Length.ToString() : "n/a"),
                    ("method", nameof(Sequence))
                });
            return this;
        } else {
            priority ??= LSProcessPriority.NORMAL; // default priority
            selectorNode = LSProcessNodeSelector.Create(nodeID, order, priority.Value, readOnly, conditions);
            // If there is no root node, this becomes the root, otherwise add to root node as child
            if (_rootNode == null) _rootNode = selectorNode;
            else _rootNode.AddChild(selectorNode);
        }

        // since we are passing a reference to the node, the builder will modify the node directly
        selectorBuilderAction?.Invoke(new LSProcessTreeBuilder(selectorNode));

        // Detailed debug logging
        LSLogger.Singleton.Debug($"Selector Node {actionDebug} [{nodeID}].",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("builderProvided", selectorBuilderAction != null),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("order", selectorNode.Order.ToString()),
                ("priority", selectorNode.Priority.ToString()),
                ("readOnly", selectorNode.ReadOnly.ToString()),
                ("conditions", conditions.Length.ToString()),
                ("overrideConditions", overrideConditions.ToString()),
                ("method", nameof(Selector))
            });

        return this;
    }

    /// <summary>
    /// Creates or navigates to a parallel node in the root node with support for nested hierarchy construction.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the parallel node within the current context.</param>
    /// <param name="parallelBuilderAction">Optional delegate for building nested children within this parallel node. If provided, the parallel node is built completely and the builder stays at the parent level.</param>
    /// <param name="numRequiredToSucceed">The number of child nodes that must succeed for the parallel node to succeed (default: 0 - all must succeed).</param>
    /// <param name="numRequiredToFailure">The number of child nodes that must fail for the parallel node to fail (default: 0 - any failure causes parallel failure).</param>
    /// <param name="priority">Processing priority level for this parallel node (default: NORMAL).</param>
    /// <param name="overrideConditions">If true, replaces existing conditions; if false, merges with existing conditions.</param>
    /// <param name="readOnly">If true, the node cannot be modified after creation.</param>
    /// <param name="conditions">Optional array of conditions that must be met before this parallel node processes.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <remarks>
    /// <para><strong>Parallel Node Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Concurrent Processing</strong>: All eligible children are processed sequentially</description></item>
    /// <item><description><strong>Threshold-Based Success</strong>: Success determined by numRequiredToSucceed threshold</description></item>
    /// <item><description><strong>Threshold-Based Failure</strong>: Failure determined by numRequiredToFailure threshold</description></item>
    /// <item><description><strong>Non-Blocking</strong>: All child nodes are executed, even when cancelling.</description></item>
    /// </list>
    /// 
    /// <para><strong>Waiting and Resume Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Individual Resume</strong>: Parallel nodes in WAITING state can be resumed individually</description></item>
    /// <item><description><strong>Child State Aggregation</strong>: Overall parallel status depends on child states and thresholds</description></item>
    /// <item><description><strong>Partial Completion</strong>: Can succeed even if some children are still waiting (NOTE: this probably is not yet fully implemented)</description></item>
    /// </list>
    /// 
    /// <para><strong>Node Creation and Replacement:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>New Node Creation</strong>: Creates a new parallel node if none exists with the given ID</description></item>
    /// <item><description><strong>Existing Node Updates</strong>: Updates success or failure thresholds of existing parallel nodes if they are not read-only</description></item>
    /// <item><description><strong>Order Preservation</strong>: Maintains original order (NOTE: this may not be the desired behavior, but would require to have another parameter)</description></item>
    /// </list>
    /// </remarks>
    public LSProcessTreeBuilder Parallel(string nodeID,
            LSProcessBuilderAction? parallelBuilderAction = null,
            int? numRequiredToSucceed = 0,
            int? numRequiredToFailure = 0,
            LSProcessPriority? priority = null,
            bool overrideConditions = false,
            bool readOnly = false,
            params LSProcessNodeCondition?[] conditions) {

        ILSProcessNode? existingNode = null;
        int order = _rootNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        var childFound = getChild(nodeID, out existingNode);
        //flow debug logging
        var actionDebug = childFound ? "update" : "create";
        LSLogger.Singleton.Debug($"{ClassName}.Parallel: {actionDebug} [{nodeID}]",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));
        if (existingNode is LSProcessNodeParallel parallelNode) {
            // update only if the node is not read-only
            if (!existingNode.ReadOnly) {
                // if fields are not provided keep the existing values
                parallelNode.NumRequiredToSucceed = numRequiredToSucceed ?? parallelNode.NumRequiredToSucceed;
                parallelNode.NumRequiredToFailure = numRequiredToFailure ?? parallelNode.NumRequiredToFailure;
                parallelNode.Priority = priority ?? parallelNode.Priority;
                parallelNode.Conditions = LSProcessHelpers.UpdateConditions(overrideConditions, parallelNode.Conditions, conditions);
            }
            //continue to build children if builder provided even if the node is read-only
        } else if (childFound) {
            // it does not make sense to modify a parallel child when the existing node is a sequence, selector or handler.
            LSLogger.Singleton.Warning($"Node [{nodeID}] exists but is not a parallel node.",
                source: (ClassName, null),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                    ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                    ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                    ("order", order),
                    ("priority", priority?.ToString() ?? "n/a"),
                    ("numRequiredToSucceed", numRequiredToSucceed?.ToString() ?? "n/a"),
                    ("numRequiredToFailure", numRequiredToFailure?.ToString() ?? "n/a"),
                    ("overrideConditions", overrideConditions),
                    ("conditions", conditions != null ? conditions.Length.ToString() : "n/a"),
                    ("method", nameof(Parallel))
                });
            return this;
        } else {
            //new parallel node
            priority ??= LSProcessPriority.NORMAL; // default priority
            numRequiredToSucceed ??= 0;
            numRequiredToFailure ??= 0;
            parallelNode = LSProcessNodeParallel.Create(nodeID, order, numRequiredToSucceed.Value, numRequiredToFailure.Value, priority.Value, readOnly, conditions);
            // If there is no root node, this becomes the root, otherwise add to root node
            if (_rootNode == null) _rootNode = parallelNode;
            else _rootNode.AddChild(parallelNode);
        }
        // since we are passing a reference to the node, the builder will modify the node directly
        parallelBuilderAction?.Invoke(new LSProcessTreeBuilder(parallelNode));
        // detailed debug logging
        LSLogger.Singleton.Debug($"Parallel Node {actionDebug} [{nodeID}].",
            source: (ClassName, null),
            processId: null,
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("action", actionDebug),
                ("builderProvided", parallelBuilderAction != null),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("order", parallelNode.Order.ToString()),
                ("priority", parallelNode.Priority.ToString()),
                ("numRequiredToSucceed", parallelNode.NumRequiredToSucceed.ToString()),
                ("numRequiredToFailure", parallelNode.NumRequiredToFailure.ToString()),
                ("conditions", conditions.Length.ToString()),
                ("overrideConditions", overrideConditions),
                ("method", nameof(Parallel))
            });

        return this;
    }

    /// <summary>
    /// Merges a sub-layer node hierarchy into the current root node.
    /// </summary>
    /// <param name="subLayer">The sub-layer node hierarchy to merge into the root node.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSArgumentNullException">Thrown when subLayer is null.</exception>
    /// <exception cref="LSException">Thrown when no current context exists and subLayer is empty.</exception>
    /// <remarks>
    /// <para><strong>Merge Strategies:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Root Seeding</strong>: If no root context exists, subLayer becomes the root</description></item>
    /// <item><description><strong>Type-Based Merging</strong>: Layer nodes with same nodeID and the same layer type are merged recursively, handlers are replaced if the target is not read-only.</description></item>
    /// </list>
    /// </remarks>
    public LSProcessTreeBuilder Merge(ILSProcessLayerNode subLayer) {
        if (subLayer == null) {
            //log warning
            LSLogger.Singleton.Warning($"Cannot merge an invalid node",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("subLayer", "n/a"),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("method", nameof(Merge))
                });
            throw new LSArgumentNullException(nameof(subLayer), "Provided subLayer is null.");
        }
        LSLogger.Singleton.Debug($"{ClassName}.Merge: [{subLayer.NodeID}] into [{_rootNode?.NodeID ?? "n/a"}]",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));
        if (_rootNode == null) {
            //NOTE: we don't need to merge recursive if the root is null, but maybe we need to check if the subLayer has children?
            //if (subLayer.GetChildren().Length == 0) throw new LSException("Provided sublayer invalid.");
            _rootNode = subLayer;
            LSLogger.Singleton.Debug($"Root node set to [{subLayer.NodeID}]",
                source: ("LSProcessSystem", null),
                properties: ("hideNodeID", true));
        } else if (_rootNode.NodeID == subLayer.NodeID) { // merging into root node if they have the same ID
            if (_rootNode.GetType() != subLayer.GetType()) {
                // different types log warning
                LSLogger.Singleton.Warning($"Node [{subLayer.NodeID}] is different from the root node.",
                    source: (ClassName, true),
                    properties: new (string, object)[] {
                        ("nodeID", subLayer.NodeID),
                        ("subLayerType", subLayer.GetType().Name),
                        ("rootType", _rootNode.GetType().Name),
                        ("method", nameof(Merge))
                    });
                return this;
            }
            LSLogger.Singleton.Debug($"Root and subLayer have the same ID [{subLayer.NodeID}], merging contents.",
                source: ("LSProcessSystem", null),
                properties: ("hideNodeID", true));
            mergeRecursive(_rootNode, subLayer);
        } else {
            var existingLayer = _rootNode.GetChild(subLayer.NodeID) as ILSProcessLayerNode;
            if (existingLayer == null) {
                // no existing node with the same ID, just add the entire subLayer directly
                LSLogger.Singleton.Debug($"Merge: [{subLayer.NodeID}] [{_rootNode.NodeID}]",
                    source: ("LSProcessSystem", null),
                    properties: ("hideNodeID", true));
                _rootNode.AddChild(subLayer);
                return this;
            } else mergeNode(_rootNode, subLayer);
        }

        return this;
    }
    /// <summary>
    /// Merges a source node into a target layer node with conflict resolution based on node types.
    /// </summary>
    /// <param name="targetLayerNode"></param>
    /// <param name="sourceNode"></param>
    private void mergeNode(ILSProcessLayerNode targetLayerNode, ILSProcessNode sourceNode) {
        var existingChild = targetLayerNode.GetChild(sourceNode.NodeID);
        if (existingChild == null) {
            targetLayerNode.AddChild(sourceNode);
            return;
        }

        // Node exists, check if both are layer nodes for recursive merging
        if (existingChild is ILSProcessLayerNode existingLayer && sourceNode is ILSProcessLayerNode subNodeLayer) {
            // Both are layer nodes - merge recursively
            mergeRecursive(existingLayer, subNodeLayer);
        } else if (existingChild.GetType() == sourceNode.GetType()) {
            // Same type but not layer nodes (e.g., both handlers)
            if (existingChild.ReadOnly) {
                LSLogger.Singleton.Warning($"Node [{sourceNode.NodeID}] exists but is read-only, cannot replace.",
                    source: (ClassName, true),
                    properties: new (string, object)[] {
                            ("nodeID", sourceNode.NodeID),
                            ("subNodeType", sourceNode.GetType().Name),
                            ("existingType", existingChild.GetType().Name),
                            ("method", nameof(mergeNode))
                });
                return;
            }

            targetLayerNode.RemoveChild(sourceNode.NodeID);
            targetLayerNode.AddChild(sourceNode);
        } else {
            // Different types - log warning and continue
            LSLogger.Singleton.Warning($"Node [{sourceNode.NodeID}] already exists but is different type.",
                source: (ClassName, true),
                properties: new (string, object)[] {
                            ("nodeID", sourceNode.NodeID),
                            ("subNodeType", sourceNode.GetType().Name),
                            ("existingType", existingChild.GetType().Name),
                            ("method", nameof(mergeNode))
                });
        }
    }

    /// <summary>
    /// Recursively merges the contents of a source node hierarchy into a target node hierarchy.
    /// In merge targetNode is always treated as readOnly so sourceNode cannot change priority or conditions.
    /// To make this work otherwise, sourceNode would have to be cast as a concrete node or the change the interface.
    /// </summary>
    /// <param name="targetNode">The target layer node that will receive merged content.</param>
    /// <param name="sourceNode">The source layer node whose content will be merged into the target.</param>
    private void mergeRecursive(ILSProcessLayerNode targetNode, ILSProcessLayerNode sourceNode) {
        // Iterate through all children of the source node
        foreach (var sourceChild in sourceNode.GetChildren()) {
            mergeNode(targetNode, sourceChild);
        }
    }

    /// <summary>
    /// Differently from others, a inverter node always has one child, so the builder action is required.
    /// </summary>
    /// <param name="nodeID"></param>
    /// <param name="builder"></param>
    /// <param name="priority"></param>
    /// <param name="conditions"></param>
    /// <returns></returns>
    public LSProcessTreeBuilder Inverter(string nodeID,
            LSProcessBuilderAction builder,
            LSProcessPriority? priority = LSProcessPriority.NORMAL,
            bool overrideConditions = false,
            bool readOnly = false,
            params LSProcessNodeCondition?[] conditions) {
        ILSProcessNode? existingNode = null;
        int order = _rootNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        var childFound = getChild(nodeID, out existingNode);
        // Flow debug logging
        var actionDebug = childFound ? "create" : "update";
        LSLogger.Singleton.Debug($"{ClassName}.Inverter: [{nodeID}] {actionDebug}.",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));

        if (existingNode is LSProcessNodeInverter inverterNode) {
            // update only if the node is not read-only
            if (!existingNode.ReadOnly) {
                // if fields are not provided keep the existing values
                priority ??= existingNode.Priority;
                // update values
                inverterNode.Priority = priority.Value;
                inverterNode.Conditions = LSProcessHelpers.UpdateConditions(overrideConditions, inverterNode.Conditions, conditions);
            }
            // continue to build children if builder provided even if the node is read-only
        } else {
            if (childFound) {
                //child is not an inverter node, log warning
                LSLogger.Singleton.Warning($"Node [{nodeID}] exists but is not an inverter node.",
                    source: (ClassName, null),
                    properties: new (string, object)[] {
                        ("nodeID", nodeID),
                        ("rootNode", _rootNode?.NodeID ?? "n/a"),
                        ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                        ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                        ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                        ("order", order),
                        ("priority", priority?.ToString() ?? "n/a"),
                        ("conditions", conditions.Length.ToString()),
                        ("method", nameof(Inverter))
                    });
                return this;
            }
            priority ??= LSProcessPriority.NORMAL; // default priority
            inverterNode = LSProcessNodeInverter.Create(nodeID, priority.Value, order, readOnly, conditions);
            if (_rootNode == null) _rootNode = inverterNode;
            else _rootNode.AddChild(inverterNode);
        }
        builder(new LSProcessTreeBuilder(inverterNode));
        // Detailed debug logging
        LSLogger.Singleton.Debug($"Node Inverter {actionDebug}",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("nodeOrder", inverterNode.Order.ToString()),
                ("nodePriority", inverterNode.Priority.ToString()),
                ("conditions", conditions.Length.ToString()),
                ("readOnly", readOnly.ToString()),
                ("overrideConditions", true),
                ("method", nameof(Inverter))
            });
        return this;
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
