namespace LSUtils.ProcessSystem;
/// <summary>
/// Core interface for all nodes in the LSProcessing system hierarchy.
/// Defines the fundamental contract for process execution, state management, and tree navigation.
/// </summary>
/// <remarks>
/// <para>This interface serves as the foundation of a Composite Pattern implementation where:</para>
/// <list type="bullet">
/// <item><description><strong>Leaf Nodes</strong>: LSProcessHandlerNode implements this interface to execute actual process handlers</description></item>
/// <item><description><strong>Composite Nodes</strong>: ILSProcessLayerNode extends this interface to manage child nodes</description></item>
/// <item><description><strong>Processing Flow</strong>: All nodes participate in a unified processing pipeline</description></item>
/// </list>
/// 
/// <para><strong>State Lifecycle:</strong></para>
/// <code>
/// UNKNOWN → [SUCCESS|FAILURE|WAITING|CANCELLED] (via Process)
/// WAITING → [SUCCESS|FAILURE|CANCELLED] (via Resume/Fail/Cancel)
/// [SUCCESS|FAILURE|CANCELLED] → [unchanged] (terminal states)
/// </code>
/// 
/// <para><strong>Design Patterns:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Composite Pattern</strong>: Uniform interface for leaf and composite nodes</description></item>
/// <item><description><strong>State Pattern</strong>: Node status drives behavior and valid operations</description></item>
/// <item><description><strong>Strategy Pattern</strong>: Conditions provide pluggable execution logic</description></item>
/// </list>
/// </remarks>
public interface ILSProcessNode {
    /// <summary>
    /// Unique identifier for this node within its parent scope.
    /// Used for navigation, debugging, and targeting Resume/Fail operations.
    /// </summary>
    /// <value>Must be unique within the parent node's children collection.</value>
    string NodeID { get; }
    /// <summary>
    /// Execution priority within parent containers.
    /// Higher priority nodes execute before lower priority nodes.
    /// </summary>
    /// <value>Priority level from LSProcessPriority enum (CRITICAL, HIGH, NORMAL, LOW, BACKGROUND).</value>
    /// <remarks>When priorities are equal, the Order property is used as a tiebreaker.</remarks>
    LSProcessPriority Priority { get; }
    /// <summary>
    /// Execution prerequisites that must be satisfied before this node can process.
    /// All conditions must return true for the node to be eligible for execution.
    /// </summary>
    /// <value>Delegate that evaluates conditions based on process and node context.</value>
    /// <remarks>
    /// Conditions are evaluated before processing and can be composed using delegate combination.
    /// Use LSProcessConditions.IsMet() to evaluate all conditions in the delegate chain.
    /// </remarks>
    LSProcessNodeCondition? Conditions { get; }
    /// <summary>
    /// Number of times this node has been executed.
    /// For handler nodes, this count is shared among clones to provide global execution statistics.
    /// </summary>
    /// <value>Execution count starting from 0.</value>
    /// <remarks>
    /// Layer nodes throw NotImplementedException as execution tracking is only meaningful for handler nodes.
    /// This property is used for analytics and debugging purposes.
    /// </remarks>
    int ExecutionCount { get; }
    /// <summary>
    /// Execution order among sibling nodes with the same priority.
    /// Lower values execute first when priorities are equal.
    /// </summary>
    /// <value>Order value set during node registration, typically incremented sequentially.</value>
    /// <remarks>This provides deterministic execution order for nodes with identical priorities.</remarks>
    int Order { get; }
    /// <summary>
    /// Creates an independent copy of this node for parallel processing or tree manipulation.
    /// </summary>
    /// <returns>New instance with the same configuration but typically without runtime state.</returns>
    /// <remarks>
    /// <para>Implementation varies by node type:</para>
    /// <list type="bullet">
    /// <item><description><strong>Handler Nodes</strong>: Usually share execution count with original via base node reference</description></item>
    /// <item><description><strong>Layer Nodes</strong>: Create deep copies of child hierarchies</description></item>
    /// </list>
    /// <para>Cloned nodes typically start with UNKNOWN status regardless of original node state.</para>
    /// </remarks>
    ILSProcessNode Clone();
    /// <summary>
    /// Primary execution method for process execution.
    /// Processes this node and returns the resulting status.
    /// </summary>
    /// <param name="context">Process execution session containing the process and execution state.</param>
    /// <returns>Processing status indicating the outcome (SUCCESS, FAILURE, WAITING, CANCELLED).</returns>
    /// <remarks>
    /// <para><strong>Execution Flow:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Condition Check</strong>: Evaluates conditions before execution</description></item>
    /// <item><description><strong>State Validation</strong>: Checks if node can be executed in current state</description></item>
    /// <item><description><strong>Processing Logic</strong>: Executes node-specific processing behavior</description></item>
    /// <item><description><strong>Result Inversion</strong>: Applies inversion if WithInverter is true</description></item>
    /// </list>
    /// 
    /// <para><strong>Behavior by Node Type:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Handler Nodes</strong>: Execute the associated handler delegate</description></item>
    /// <item><description><strong>Layer Nodes</strong>: Process children according to their logic (sequence, selector, parallel)</description></item>
    /// </list>
    /// 
    /// <para><strong>State Management:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Idempotency</strong>: May return cached results for terminal states (SUCCESS, FAILURE, CANCELLED)</description></item>
    /// <item><description><strong>Side Effects</strong>: May modify internal node state and increment execution count</description></item>
    /// <item><description><strong>Thread Safety</strong>: Implementations should ensure thread-safe execution</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus Execute(LSProcessSession context);
    /// <summary>
    /// Non-destructive inquiry of the current processing status.
    /// Returns the current state without side effects or state changes.
    /// </summary>
    /// <returns>Current processing status of this node.</returns>
    /// <remarks>
    /// <para>This method should be lightweight and thread-safe for concurrent reads.</para>
    /// <para><strong>Status Meanings:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>UNKNOWN</strong>: Initial state, not yet processed</description></item>
    /// <item><description><strong>SUCCESS</strong>: Completed successfully</description></item>
    /// <item><description><strong>FAILURE</strong>: Completed with failure</description></item>
    /// <item><description><strong>WAITING</strong>: Blocked, requires external Resume/Fail to continue</description></item>
    /// <item><description><strong>CANCELLED</strong>: Terminated, cannot be resumed</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus GetNodeStatus();
    /// <summary>
    /// Continues processing from WAITING state for this node or specified child nodes.
    /// </summary>
    /// <param name="context">Processing session for continuation.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for resumption. If null or empty, resumes all waiting children.</param>
    /// <returns>New processing status after resume attempt.</returns>
    /// <remarks>
    /// <para><strong>Resumption Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>State Validation</strong>: Only WAITING nodes can be resumed</description></item>
    /// <item><description><strong>Target Selection</strong>: Specific nodes or all waiting children based on parameters</description></item>
    /// <item><description><strong>Cascading Effects</strong>: May trigger parent node status recalculation</description></item>
    /// </list>
    /// 
    /// <para><strong>Behavior by Node Type:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Handler Nodes</strong>: Transitions from WAITING to SUCCESS if applicable</description></item>
    /// <item><description><strong>Layer Nodes</strong>: Delegates to appropriate waiting children</description></item>
    /// </list>
    /// 
    /// <para><strong>Error Handling:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Invalid States</strong>: Returns current status if node is not in WAITING state</description></item>
    /// <item><description><strong>Missing Targets</strong>: Ignores non-existent or non-waiting node IDs</description></item>
    /// <item><description><strong>Partial Success</strong>: May resume some nodes even if others cannot be resumed</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus Resume(LSProcessSession context, params string[]? nodes);
    /// <summary>
    /// Forces transition from WAITING to FAILURE state for this node or specified child nodes.
    /// </summary>
    /// <param name="context">Processing session for state transition.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for failure. If null or empty, fails all waiting children.</param>
    /// <returns>New processing status after failure operation.</returns>
    /// <remarks>
    /// <para><strong>Failure Injection Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>State Transition</strong>: Forces WAITING nodes to FAILURE state</description></item>
    /// <item><description><strong>Target Selection</strong>: Specific nodes or all waiting children based on parameters</description></item>
    /// <item><description><strong>Immediate Effect</strong>: State change is applied immediately without further processing</description></item>
    /// </list>
    /// 
    /// <para><strong>Cascading Effects:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Parent Aggregation</strong>: May trigger parent node status changes based on their logic</description></item>
    /// <item><description><strong>Sibling Impact</strong>: May affect sibling processing in sequence/selector nodes</description></item>
    /// <item><description><strong>Session State</strong>: May update overall session status and completion state</description></item>
    /// </list>
    /// 
    /// <para><strong>Common Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Timeout Handling</strong>: Force failure when operations exceed time limits</description></item>
    /// <item><description><strong>Error Conditions</strong>: Propagate external errors to waiting nodes</description></item>
    /// <item><description><strong>Resource Constraints</strong>: Fail operations when resources become unavailable</description></item>
    /// <item><description><strong>User Cancellation</strong>: Handle explicit user-initiated cancellations</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus Fail(LSProcessSession context, params string[]? nodes);
    /// <summary>
    /// Terminates processing with CANCELLED state for this node and its entire subtree.
    /// </summary>
    /// <param name="context">Processing session for cancellation.</param>
    /// <returns>Status after cancellation, should typically be CANCELLED.</returns>
    /// <remarks>
    /// <para><strong>Cancellation Scope:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Recursive Effect</strong>: Affects entire subtree rooted at this node</description></item>
    /// <item><description><strong>Immediate Termination</strong>: Stops all ongoing and pending operations</description></item>
    /// <item><description><strong>State Propagation</strong>: CANCELLED state propagates to all child nodes</description></item>
    /// </list>
    /// 
    /// <para><strong>Cancellation Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Terminal State</strong>: CANCELLED is typically a final state that cannot be resumed</description></item>
    /// <item><description><strong>Resource Cleanup</strong>: Implementations should clean up resources and release locks</description></item>
    /// <item><description><strong>Graceful Shutdown</strong>: Should complete current atomic operations before cancelling</description></item>
    /// </list>
    /// 
    /// <para><strong>Implementation Requirements:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Idempotency</strong>: Should be safe to call multiple times without side effects</description></item>
    /// <item><description><strong>Thread Safety</strong>: Must handle concurrent cancellation requests safely</description></item>
    /// <item><description><strong>Status Consistency</strong>: Should ensure consistent state across the node hierarchy</description></item>
    /// </list>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>User Cancellation</strong>: Handle explicit user-initiated cancellation requests</description></item>
    /// <item><description><strong>System Shutdown</strong>: Gracefully terminate processing during system shutdown</description></item>
    /// <item><description><strong>Error Recovery</strong>: Cancel operations when unrecoverable errors occur</description></item>
    /// <item><description><strong>Resource Limits</strong>: Terminate processing when resource limits are exceeded</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus Cancel(LSProcessSession context);
}
