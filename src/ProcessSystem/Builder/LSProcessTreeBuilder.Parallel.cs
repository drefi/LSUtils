using System.Linq;
using LSUtils.Logging;

namespace LSUtils.ProcessSystem;

public partial class LSProcessTreeBuilder {
    /// <summary>
    /// Creates or navigates to a parallel node in the root node with support for nested hierarchy construction.
    /// Parallel nodes process all eligible children and determine overall success/failure based on configurable thresholds.
    /// Unlike sequences/selectors, parallel nodes do not short-circuit and always execute all children.
    /// This not means that the children are executed concurrently, but rather that all children are processed in sequence without early termination.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the parallel node within the current context.</param>
    /// <param name="builderAction">Optional delegate for building nested children within this parallel node. If provided, the parallel node is built completely and the builder stays at the parent level.</param>
    /// <param name="successThreshold">The number of child nodes that must succeed for the parallel node to succeed (default: 0 - all must succeed).</param>
    /// <param name="failureThreshold">The number of child nodes that must fail for the parallel node to fail (default: 0 - any failure causes parallel failure).</param>
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
            LSProcessBuilderAction? builderAction = null,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            int successThreshold = 0,
            int failureThreshold = 0,
            LSProcessNodeParallel.ParallelThresholdMode thresholdMode = LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition?[] conditions) {
        if (_rootNode == null) {
            LSLogger.Singleton.Error($"Root not found [{nodeID}].",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("method", nameof(Parallel))
                });
            return this;
        }
        var order = _rootNode.GetChildren().Length;
        if (getChild(nodeID, out ILSProcessNode? existingNode) == false || existingNode == null) { // no child with same nodeID existing, creating new node
            var newParallelNode = new LSProcessNodeParallel(nodeID,
                order,
                successThreshold,
                failureThreshold,
                thresholdMode,
                updatePolicy,
                priority,
                conditions);
            _rootNode.AddChild(newParallelNode);
            // builder action is invoked with a new builder context at the new parallel node
            builderAction?.Invoke(new LSProcessTreeBuilder(newParallelNode));
            LSLogger.Singleton.Debug($"New parallel node [{nodeID}] created.",
                source: (ClassName, null),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("order", order),
                    ("priority", priority.ToString()),
                    ("updatePolicy", updatePolicy.ToString()),
                    ("conditions", conditions.Length.ToString()),
                    ("method", nameof(Selector))
                });
            return this;
        }

        var policyOrder = existingNode.Order; // preserve order of existing node
        var policyPriority = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_PRIORITY) ? priority : existingNode.Priority;
        var policyConditions = LSProcessHelpers.UpdateConditions(updatePolicy, existingNode.Conditions, conditions);
        var policyChildren = System.Array.Empty<ILSProcessNode>();
        var policyUpdatePolicy = (existingNode.UpdatePolicy | updatePolicy) & (NodeUpdatePolicy.IGNORE_CHANGES | NodeUpdatePolicy.IGNORE_BUILDER);

        var policySuccessThreshold = successThreshold;
        var policyFailureThreshold = failureThreshold;
        var policyThresholdMode = thresholdMode;

        //flow debug logging

        if (existingNode is not LSProcessNodeParallel parallelNode) { // existing node is not a parallel node
            if (existingNode is ILSProcessLayerNode existingLayerNode) {
                if (updatePolicy.HasFlag(NodeUpdatePolicy.REPLACE_LAYER) == false || // not replacing the layer
                    existingLayerNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES)) { // existingLayerNode is read-only

                    LSLogger.Singleton.Warning($"Node [{nodeID}] is read-only.",
                        source: (ClassName, true),
                        properties: new (string, object)[] {
                        ("nodeID", nodeID),
                        ("rootNode", _rootNode?.NodeID ?? "n/a"),
                        ("existingLayerNode", existingLayerNode.GetType().Name ?? "n/a"),
                        ("existingLayerNodeOrder", existingLayerNode.Order.ToString() ?? "n/a"),
                        ("existingLayerNodePriority", existingLayerNode.Priority.ToString() ?? "n/a"),
                        ("existingLayerNodeUpdatePolicy", existingLayerNode.UpdatePolicy.ToString()),
                        ("existingLayerNodeConditions", existingLayerNode.Conditions != null ? existingLayerNode.Conditions.Length.ToString() : "n/a"),
                        ("order", order),
                        ("priority", priority.ToString()),
                        ("updatePolicy", updatePolicy.ToString()),
                        ("conditions", conditions != null ? conditions.Length.ToString() : "n/a"),
                        ("method", nameof(Parallel))
                    });

                    if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_BUILDER) == false) { // existing node allows builder updates
                        // continue to build children if the existing node allows it
                        builderAction?.Invoke(new LSProcessTreeBuilder(existingLayerNode));
                    }
                    return this; // existing node is read-only, exit early
                }
            }
        } else {
            // existing node is a parallel node, update thresholds based on update policy
            if (updatePolicy.HasFlag(NodeUpdatePolicy.REPLACE_LAYER) == false || // not replacing the layer
                parallelNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES)) { // existingLayerNode is read-only

                if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_BUILDER) == false) {  // existing node allows builder updates
                    builderAction?.Invoke(new LSProcessTreeBuilder(parallelNode));
                }
                return this;
            }
            // preserve existing children if not replacing layer
            policyChildren = parallelNode.GetChildren();
            policySuccessThreshold = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_PARALLEL_NUM_SUCCESS) ? successThreshold : parallelNode.SuccessThreshold;
            policyFailureThreshold = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_PARALLEL_NUM_FAILURE) ? failureThreshold : parallelNode.FailureThreshold;
            policyThresholdMode = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_THRESHOLD_MODE) ? thresholdMode : parallelNode.ThresholdMode;
        }

        parallelNode = new LSProcessNodeParallel(nodeID,
            policyOrder,
            policySuccessThreshold,
            policyFailureThreshold,
            policyThresholdMode,
            policyUpdatePolicy,
            policyPriority,
            policyConditions);

        if (policyChildren.Length > 0) parallelNode.AddChildren(policyChildren);
        _rootNode.RemoveChild(nodeID);
        _rootNode.AddChild(parallelNode);

        if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_BUILDER) == false)
            builderAction?.Invoke(new LSProcessTreeBuilder(parallelNode));

        LSLogger.Singleton.Debug($"Parallel node [{nodeID}] updated.",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("order", parallelNode.Order.ToString()),
                ("priority", parallelNode.Priority.ToString()),
                ("updatePolicy", parallelNode.UpdatePolicy.ToString()),
                ("successThreshold", parallelNode.SuccessThreshold.ToString()),
                ("failureThreshold", parallelNode.FailureThreshold.ToString()),
                ("thresholdMode", parallelNode.ThresholdMode.ToString()),
                ("conditions", parallelNode.Conditions.Length.ToString()),
                ("method", nameof(Parallel))
            });

        return this;
    }
    public LSProcessTreeBuilder Parallel(LSProcessBuilderAction? builderAction = null,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            int successThreshold = 0,
            int failureThreshold = 0,
            LSProcessNodeParallel.ParallelThresholdMode thresholdMode = LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition?[] conditions) {
        string nodeID = $"{_rootNode?.NodeID ?? "root"}-parallel-{System.Guid.NewGuid()}";
        return Parallel(nodeID,
            builderAction,
            updatePolicy,
            successThreshold,
            failureThreshold,
            thresholdMode,
            priority,
            conditions);
    }

    /// <summary>
    /// Creates a type-safe parallel node for strongly-typed processes.
    /// </summary>
    /// <typeparam name="TProcess">The specific process type for compile-time type safety.</typeparam>
    /// <param name="nodeID">Unique identifier for the parallel node.</param>
    /// <param name="parallelBuilderAction">Optional action to configure child nodes within the parallel context.</param>
    /// <param name="numRequiredToSucceed">Number of children that must succeed for parallel success (default: 0 = all children).</param>
    /// <param name="numRequiredToFailure">Number of children that must fail for parallel failure (default: 0 = any child).</param>
    /// <param name="priority">Execution priority (default: NORMAL).</param>
    /// <param name="overrideConditions">If true, replaces existing conditions; if false, appends to them.</param>
    /// <param name="readOnly">If true, prevents further modifications to the node.</param>
    /// <param name="conditions">Optional array of type-safe conditions for execution.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Parallel&lt;MyProcess&gt;("concurrent", par => par
    ///     .Handler&lt;MyProcess&gt;("task1", Task1Handler)
    ///     .Handler&lt;MyProcess&gt;("task2", Task2Handler),
    ///     numRequiredToSucceed: 2,
    ///     conditions: process => process.IsEnabled);
    /// </code>
    /// </example>
    public LSProcessTreeBuilder Parallel<TProcess>(string nodeID,
            LSProcessBuilderAction? builderAction = null,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            int successThreshold = 0,
            int failureThreshold = 0,
            LSProcessNodeParallel.ParallelThresholdMode thresholdMode = LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition<TProcess>?[] conditions)
        where TProcess : LSProcess {

        // Convert generic conditions to non-generic
        var convertedConditions = conditions
            .Where(c => c != null)
            .Select(c => c!.ToCondition())
            .ToArray();

        return Parallel(nodeID,
            builderAction,
            updatePolicy,
            successThreshold,
            failureThreshold,
            thresholdMode,
            priority,
            convertedConditions
        );
    }
    public LSProcessTreeBuilder Parallel<TProcess>(LSProcessBuilderAction? builderAction = null,
        NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            int successThreshold = 0,
            int failureThreshold = 0,
            LSProcessNodeParallel.ParallelThresholdMode thresholdMode = LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition<TProcess>?[] conditions)
        where TProcess : LSProcess {

        string nodeID = $"{_rootNode?.NodeID ?? "root"}-parallel-{System.Guid.NewGuid()}";
        return Parallel(nodeID,
            builderAction,
            updatePolicy,
            successThreshold,
            failureThreshold,
            thresholdMode,
            priority,
            conditions);
    }
}
