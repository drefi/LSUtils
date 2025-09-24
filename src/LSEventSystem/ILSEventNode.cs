namespace LSUtils.EventSystem;

/// <summary>
/// Core interface for all nodes in the LSEventSystem v5 hierarchy.
/// Defines the fundamental contract for event processing, state management, and tree navigation.
/// </summary>
/// <remarks>
/// <para>This interface serves as the foundation of a Composite Pattern implementation where:</para>
/// <list type="bullet">
/// <item><description><strong>Leaf Nodes</strong>: LSEventHandlerNode implements this interface to execute actual event handlers</description></item>
/// <item><description><strong>Composite Nodes</strong>: ILSEventLayerNode extends this interface to manage child nodes</description></item>
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
public interface ILSEventNode {
    /// <summary>
    /// Unique identifier for this node within its parent scope.
    /// Used for navigation, debugging, and targeting Resume/Fail operations.
    /// </summary>
    /// <value>Must be unique within the parent node's children collection.</value>
    string NodeID { get; }  // Used as path identifier for navigation

    /// <summary>
    /// Execution priority within parent containers.
    /// Higher priority nodes execute before lower priority nodes.
    /// </summary>
    /// <value>Priority level from LSPriority enum (CRITICAL, HIGH, NORMAL, LOW).</value>
    /// <remarks>When priorities are equal, the Order property is used as a tiebreaker.</remarks>
    LSPriority Priority { get; }

    /// <summary>
    /// Execution prerequisites that must be satisfied before this node can process.
    /// All conditions must return true for the node to be eligible for execution.
    /// </summary>
    /// <value>Delegate that evaluates conditions based on event and node context.</value>
    /// <remarks>
    /// Conditions are evaluated before processing and can be composed using delegate combination.
    /// Use LSEventConditions.IsMet() to evaluate all conditions in the delegate chain.
    /// </remarks>
    // Conditions to be met to execute this node, a node that does not meet conditions should be skipped
    LSEventCondition? Conditions { get; }

    /// <summary>
    /// Number of times this node has been executed.
    /// For handler nodes, this count is shared among clones to provide global execution statistics.
    /// </summary>
    /// <value>Execution count starting from 0.</value>
    /// <remarks>
    /// Layer nodes throw NotImplementedException as execution tracking is only meaningful for handler nodes.
    /// This property is used for analytics and debugging purposes.
    /// </remarks>
    int ExecutionCount { get; } // Number of times this node has been executed

    /// <summary>
    /// Execution order among sibling nodes with the same priority.
    /// Lower values execute first when priorities are equal.
    /// </summary>
    /// <value>Order value set during node registration, typically incremented sequentially.</value>
    /// <remarks>This provides deterministic execution order for nodes with identical priorities.</remarks>
    int Order { get; } // Order of execution among nodes, this is set during registration order (increased)

    bool WithInverter { get; }

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
    ILSEventNode Clone();

    /// <summary>
    /// Primary execution method for event processing.
    /// Processes this node and returns the resulting status.
    /// </summary>
    /// <param name="context">Event processing context containing the event and root node reference.</param>
    /// <returns>Processing status indicating the outcome (SUCCESS, FAILURE, WAITING, CANCELLED).</returns>
    /// <remarks>
    /// <para><strong>Behavior by Node Type:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Handler Nodes</strong>: Execute the associated handler delegate</description></item>
    /// <item><description><strong>Layer Nodes</strong>: Process children according to their logic (sequence, selector, parallel)</description></item>
    /// </list>
    /// <para><strong>Idempotency:</strong> May return cached results for terminal states (SUCCESS, FAILURE, CANCELLED).</para>
    /// <para><strong>Side Effects:</strong> May modify internal node state and increment execution count.</para>
    /// </remarks>
    //LSEventProcessStatus Process(LSEventProcessContext context, params string[]? nodes);
    LSEventProcessStatus Process(LSEventProcessContext context);

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
    LSEventProcessStatus GetNodeStatus();

    /// <summary>
    /// Continues processing from WAITING state for this node or specified child nodes.
    /// </summary>
    /// <param name="context">Processing context for continuation.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for resumption. If null or empty, resumes all waiting children.</param>
    /// <returns>New processing status after resume attempt.</returns>
    /// <remarks>
    /// <para><strong>Preconditions:</strong> Node or specified children should be in WAITING state.</para>
    /// <para><strong>Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Handler Nodes</strong>: Transitions from WAITING to SUCCESS if applicable</description></item>
    /// <item><description><strong>Layer Nodes</strong>: Delegates to appropriate waiting children</description></item>
    /// </list>
    /// <para><strong>Invalid States:</strong> Returns current status if node is not in WAITING state.</para>
    /// </remarks>
    LSEventProcessStatus Resume(LSEventProcessContext context, params string[]? nodes);

    /// <summary>
    /// Forces transition from WAITING to FAILURE state for this node or specified child nodes.
    /// </summary>
    /// <param name="context">Processing context for state transition.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for failure. If null or empty, fails all waiting children.</param>
    /// <returns>New processing status after failure operation.</returns>
    /// <remarks>
    /// <para><strong>Preconditions:</strong> Node or specified children should be in WAITING state.</para>
    /// <para><strong>Cascading Effects:</strong> May trigger parent node status changes based on their logic.</para>
    /// <para><strong>Use Cases:</strong> Timeout handling, error conditions, or explicit failure injection.</para>
    /// </remarks>
    LSEventProcessStatus Fail(LSEventProcessContext context, params string[]? nodes);

    /// <summary>
    /// Terminates processing with CANCELLED state for this node and its entire subtree.
    /// </summary>
    /// <param name="context">Processing context for cancellation.</param>
    /// <returns>Status after cancellation, should typically be CANCELLED.</returns>
    /// <remarks>
    /// <para><strong>Scope:</strong> Affects entire subtree rooted at this node.</para>
    /// <para><strong>Finality:</strong> CANCELLED is typically a terminal state that cannot be resumed.</para>
    /// <para><strong>Idempotency:</strong> Should be safe to call multiple times.</para>
    /// </remarks>
    LSEventProcessStatus Cancel(LSEventProcessContext context);

}
