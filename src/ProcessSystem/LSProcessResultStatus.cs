namespace LSUtils.ProcessSystem;

/// <summary>
/// Enumeration defining the execution status of processing nodes in the LSProcessing system.
/// <para>
/// LSProcessResultStatus represents the lifecycle stages and outcomes of node execution,
/// providing consistent status semantics across all node types. Status values control
/// processing flow, parent node aggregation logic, and determine whether operations
/// can continue or require external intervention.
/// </para>
/// <para>
/// <b>State Transitions:</b><br/>
/// - UNKNOWN → {SUCCESS, FAILURE, WAITING, CANCELLED} via Execute()<br/>
/// - WAITING → {SUCCESS, FAILURE, CANCELLED} via Resume()/Fail()/Cancel()<br/>
/// - SUCCESS, FAILURE, CANCELLED → no change (terminal states)
/// </para>
/// <para>
/// <b>Aggregation Semantics by Node Type:</b><br/>
/// - Handler nodes: return delegate execution result directly<br/>
/// - Sequence nodes: AND logic (all children must succeed)<br/>
/// - Selector nodes: OR logic (any child success succeeds)<br/>  
/// - Parallel nodes: threshold-based logic (configurable success/failure counts)<br/>
/// - Inverter nodes: SUCCESS↔FAILURE inversion, others pass through
/// </para>
/// <para>
/// <b>Priority in Status Aggregation:</b><br/>
/// When layer nodes aggregate multiple child statuses, they typically prioritize:
/// CANCELLED > WAITING > FAILURE > SUCCESS > UNKNOWN
/// </para>
/// </summary>
public enum LSProcessResultStatus {
    /// <summary>
    /// Initial state before any processing has occurred.
    /// Used only for initialization and indicates the node has not been processed yet.
    /// </summary>
    /// <remarks>
    /// <b>Initialization State:</b><br/>
    /// All nodes start in this state when created or cloned. This state should transition to another state after the first Execute() call.<br/>
    /// <br/>
    /// <b>Usage Context:</b><br/>
    /// • Node creation and initialization<br/>
    /// • Before first processing attempt<br/>
    /// • Clone operations copy this initial state<br/>
    /// • Layer nodes return UNKNOWN before processing begins (_isProcessing = false)
    /// </remarks>
    UNKNOWN, // only used for initialization
    
    /// <summary>
    /// Processing completed successfully without errors.
    /// This is a terminal state indicating successful completion.
    /// </summary>
    /// <remarks>
    /// <b>Success Conditions by Node Type:</b><br/>
    /// • <b>Handler Nodes:</b> The handler delegate returned SUCCESS<br/>
    /// • <b>Sequence Nodes:</b> All children processed successfully (AND logic)<br/>
    /// • <b>Selector Nodes:</b> At least one child processed successfully (OR logic)<br/>
    /// • <b>Parallel Nodes:</b> Required number of children succeeded (threshold-based)<br/>
    /// <br/>
    /// <b>Terminal State Characteristics:</b><br/>
    /// • No further processing required for this node<br/>
    /// • State does not change once SUCCESS is achieved<br/>
    /// • Parent nodes can proceed with their processing logic<br/>
    /// • Contributes to parent success evaluation in layer nodes
    /// </remarks>
    SUCCESS, // processed successfully
    
    /// <summary>
    /// Processing completed with failure or encountered an error condition.
    /// This is a terminal state indicating unsuccessful completion.
    /// </summary>
    /// <remarks>
    /// <b>Failure Conditions by Node Type:</b><br/>
    /// • <b>Handler Nodes:</b> The handler delegate returned FAILURE<br/>
    /// • <b>Sequence Nodes:</b> At least one child failed (short-circuit on first failure)<br/>
    /// • <b>Selector Nodes:</b> All children failed (continues until all attempts exhausted)<br/>
    /// • <b>Parallel Nodes:</b> Required number of children failed (threshold-based)<br/>
    /// <br/>
    /// <b>Terminal State Characteristics:</b><br/>
    /// • Processing stopped due to error or failed condition<br/>
    /// • State does not change once FAILURE is achieved<br/>
    /// • May trigger parent node failure depending on parent logic<br/>
    /// • Can be inverted using WithInverter flag (FAILURE becomes SUCCESS)
    /// </remarks>
    FAILURE, // processed with failure
    
    /// <summary>
    /// Processing is blocked and waiting for external intervention to continue.
    /// This state requires Resume() or Fail() calls to transition to a terminal state.
    /// </summary>
    /// <remarks>
    /// <b>Common Use Cases:</b><br/>
    /// • Asynchronous operations waiting for completion<br/>
    /// • User input or external events required<br/>
    /// • Resource dependencies not yet available<br/>
    /// • Network requests or database operations in progress<br/>
    /// • File system operations or hardware interactions pending<br/>
    /// <br/>
    /// <b>Resolution Mechanisms:</b><br/>
    /// • <b>Resume():</b> Continue processing when external condition is met<br/>
    /// • <b>Fail():</b> Force failure if external condition cannot be satisfied<br/>
    /// • <b>Cancel():</b> Terminate processing without completion<br/>
    /// <br/>
    /// <b>Layer Node Behavior:</b><br/>
    /// • Sequence/Selector nodes delegate Resume/Fail to waiting children<br/>
    /// • Parallel nodes can have multiple waiting children simultaneously<br/>
    /// • Parent nodes remain WAITING while any child is WAITING
    /// </remarks>
    WAITING, // waiting for an external event to resume processing
    
    /// <summary>
    /// Processing was terminated before completion due to cancellation.
    /// This is a terminal state that cannot be resumed or continued.
    /// </summary>
    /// <remarks>
    /// <b>Cancellation Characteristics:</b><br/>
    /// • <b>Scope:</b> Cancellation typically affects entire subtrees, not individual nodes<br/>
    /// • <b>Finality:</b> Unlike WAITING, CANCELLED cannot be resumed or changed<br/>
    /// • <b>Propagation:</b> Child cancellation may trigger parent cancellation depending on logic<br/>
    /// <br/>
    /// <b>Cancellation Sources:</b><br/>
    /// • Explicit Cancel() method calls<br/>
    /// • System shutdown or resource constraints<br/>
    /// • User-initiated cancellation requests<br/>
    /// • Timeout conditions or deadline expiration<br/>
    /// • Error conditions requiring immediate termination<br/>
    /// <br/>
    /// <b>Status Priority:</b><br/>
    /// CANCELLED typically takes highest priority in status aggregation, overriding SUCCESS, FAILURE, and WAITING states in parent nodes.
    /// </remarks>
    CANCELLED // processing was cancelled
}
