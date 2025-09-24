namespace LSUtils.EventSystem;

/// <summary>
/// Enumeration defining the possible states of event processing nodes.
/// Represents the lifecycle stages and outcomes of event processing operations.
/// </summary>
/// <remarks>
/// <para><strong>State Transitions:</strong></para>
/// <code>
/// UNKNOWN → [SUCCESS|FAILURE|WAITING|CANCELLED] (via Process)
/// WAITING → [SUCCESS|FAILURE|CANCELLED] (via Resume/Fail/Cancel)
/// [SUCCESS|FAILURE|CANCELLED] → [unchanged] (terminal states)
/// </code>
/// 
/// <para><strong>Terminal States:</strong> SUCCESS, FAILURE, and CANCELLED are terminal states</para>
/// <para>that typically do not transition to other states once reached.</para>
/// 
/// <para><strong>Processing Logic:</strong> Layer nodes aggregate child statuses according to their</para>
/// <para>specific logic (sequence AND, selector OR, parallel threshold-based).</para>
/// </remarks>
public enum LSEventProcessStatus {
    /// <summary>
    /// Initial state before any processing has occurred.
    /// Used only for initialization and indicates the node has not been processed yet.
    /// </summary>
    /// <remarks>
    /// All nodes start in this state when created or cloned.
    /// This state should transition to another state after the first Process() call.
    /// </remarks>
    UNKNOWN, // only used for initialization
    
    /// <summary>
    /// Processing completed successfully without errors.
    /// This is a terminal state indicating successful completion.
    /// </summary>
    /// <remarks>
    /// <para><strong>Handler Nodes</strong>: The handler delegate returned SUCCESS</para>
    /// <para><strong>Sequence Nodes</strong>: All children processed successfully</para>
    /// <para><strong>Selector Nodes</strong>: At least one child processed successfully</para>
    /// <para><strong>Parallel Nodes</strong>: Required number of children succeeded</para>
    /// </remarks>
    SUCCESS, // processed successfully
    
    /// <summary>
    /// Processing completed with failure or encountered an error condition.
    /// This is a terminal state indicating unsuccessful completion.
    /// </summary>
    /// <remarks>
    /// <para><strong>Handler Nodes</strong>: The handler delegate returned FAILURE</para>
    /// <para><strong>Sequence Nodes</strong>: At least one child failed</para>
    /// <para><strong>Selector Nodes</strong>: All children failed</para>
    /// <para><strong>Parallel Nodes</strong>: Required number of children failed</para>
    /// </remarks>
    FAILURE, // processed with failure
    
    /// <summary>
    /// Processing is blocked and waiting for external intervention to continue.
    /// This state requires Resume() or Fail() calls to transition to a terminal state.
    /// </summary>
    /// <remarks>
    /// <para><strong>Use Cases</strong>:</para>
    /// <list type="bullet">
    /// <item><description>Asynchronous operations waiting for completion</description></item>
    /// <item><description>User input or external events required</description></item>
    /// <item><description>Resource dependencies not yet available</description></item>
    /// </list>
    /// 
    /// <para><strong>Resolution</strong>: External code must call Resume() to continue or Fail() to force failure.</para>
    /// </remarks>
    WAITING, // waiting for an external event to resume processing
    
    /// <summary>
    /// Processing was terminated before completion due to cancellation.
    /// This is a terminal state that cannot be resumed or continued.
    /// </summary>
    /// <remarks>
    /// <para><strong>Scope</strong>: Cancellation typically affects entire subtrees, not individual nodes.</para>
    /// <para><strong>Finality</strong>: Unlike WAITING, CANCELLED cannot be resumed or changed.</para>
    /// <para><strong>Propagation</strong>: Child cancellation may trigger parent cancellation depending on logic.</para>
    /// </remarks>
    CANCELLED // processing was cancelled
}
