namespace LSUtils.Processing;

/// <summary>
/// Extended interface for composite nodes that can contain and manage child nodes in the process execution hierarchy.
/// Implements the Composite Pattern by extending ILSProcessNode with child management capabilities.
/// </summary>
/// <remarks>
/// <para><strong>Composite Pattern Implementation:</strong></para>
/// <para>Layer nodes serve as composite objects in the Composite Pattern, providing uniform</para>
/// <para>treatment of individual nodes and compositions of nodes. This enables building complex</para>
/// <para>hierarchical processing structures with consistent interfaces.</para>
/// 
/// <para><strong>Node Type Implementations:</strong></para>
/// <list type="bullet">
/// <item><description><strong>LSProcessSequenceNode</strong>: Processes children sequentially until one fails (AND logic)</description></item>
/// <item><description><strong>LSProcessSelectorNode</strong>: Processes children sequentially until one succeeds (OR logic)</description></item>
/// <item><description><strong>LSProcessParallelNode</strong>: Processes children simultaneously with threshold-based completion</description></item>
/// </list>
/// 
/// <para><strong>Child Management Architecture:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Dictionary Storage</strong>: Children stored keyed by NodeID for O(1) lookup performance</description></item>
/// <item><description><strong>Priority-Based Ordering</strong>: Processing order determined by Priority (descending) then Order (ascending)</description></item>
/// <item><description><strong>Condition Filtering</strong>: Condition evaluation determines child eligibility for processing</description></item>
/// <item><description><strong>Dynamic Hierarchy</strong>: Support for runtime child addition, removal, and modification</description></item>
/// </list>
/// 
/// <para><strong>Processing Patterns:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Unified Processing</strong>: All layer nodes follow consistent pattern with _availableChildren and _processStack</description></item>
/// <item><description><strong>Status Aggregation</strong>: Logic varies by implementation (sequence vs selector vs parallel)</description></item>
/// <item><description><strong>State Delegation</strong>: Resume/Fail operations delegate to appropriate waiting child nodes</description></item>
/// <item><description><strong>Recursive Operations</strong>: Support for nested layer nodes and deep hierarchy traversal</description></item>
/// </list>
/// 
/// <para><strong>Design Benefits:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Flexibility</strong>: Build complex processing workflows from simple building blocks</description></item>
/// <item><description><strong>Reusability</strong>: Layer nodes can be composed and reused in different contexts</description></item>
/// <item><description><strong>Maintainability</strong>: Uniform interface simplifies debugging and modification</description></item>
/// <item><description><strong>Scalability</strong>: Efficient operations support large hierarchies</description></item>
/// </list>
/// </remarks>
public interface ILSProcessLayerNode : ILSProcessNode {
    /// <summary>
    /// Adds a child node to this layer node's collection.
    /// The child will be processed according to this node's processing logic.
    /// </summary>
    /// <param name="child">The child node to add. Must have a unique NodeID within this parent.</param>
    /// <remarks>
    /// <para><strong>Child Integration:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Uniqueness Enforcement</strong>: If a child with the same NodeID exists, it will be replaced</description></item>
    /// <item><description><strong>Hierarchy Building</strong>: Supports both leaf nodes (handlers) and composite nodes (layers)</description></item>
    /// <item><description><strong>Processing Integration</strong>: Child becomes part of this node's processing workflow</description></item>
    /// </list>
    /// 
    /// <para><strong>Processing Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Priority Ordering</strong>: Children processed by Priority (descending) then Order (ascending)</description></item>
    /// <item><description><strong>Condition Evaluation</strong>: Child conditions checked before inclusion in processing</description></item>
    /// <item><description><strong>State Management</strong>: Child state affects parent aggregated status</description></item>
    /// </list>
    /// 
    /// <para><strong>Runtime Restrictions:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Processing State</strong>: Some implementations prevent modifications during active processing</description></item>
    /// <item><description><strong>Thread Safety</strong>: Implementations should ensure thread-safe child management</description></item>
    /// </list>
    /// </remarks>
    void AddChild(ILSProcessNode child);
    
    /// <summary>
    /// Retrieves a child node by its unique identifier.
    /// </summary>
    /// <param name="nodeID">The unique identifier of the child node to retrieve.</param>
    /// <returns>The child node with the specified ID, or null if not found.</returns>
    /// <remarks>This operation is O(1) due to dictionary-based storage.</remarks>
    ILSProcessNode? GetChild(string nodeID);
    
    /// <summary>
    /// Checks whether a child node with the specified ID exists in this layer node.
    /// </summary>
    /// <param name="nodeID">The unique identifier to check for existence.</param>
    /// <returns>True if a child with the specified ID exists, false otherwise.</returns>
    /// <remarks>This operation is O(1) and is useful for conditional child access.</remarks>
    bool HasChild(string nodeID);
    
    /// <summary>
    /// Returns an array containing all child nodes in this layer node.
    /// Used for navigation, cloning, and status aggregation operations.
    /// </summary>
    /// <returns>Array of all child nodes. Order is not guaranteed to match processing order.</returns>
    /// <remarks>
    /// <para><strong>Snapshot Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Point-in-Time</strong>: Returns a snapshot of children at the time of call</description></item>
    /// <item><description><strong>Immutable Result</strong>: Modifying the returned array does not affect the parent's collection</description></item>
    /// <item><description><strong>Thread Safety</strong>: Safe to call during processing as it returns a copy</description></item>
    /// </list>
    /// 
    /// <para><strong>Usage Scenarios:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Hierarchy Navigation</strong>: Traverse the node tree for analysis or debugging</description></item>
    /// <item><description><strong>Cloning Operations</strong>: Deep copy child hierarchies for independent processing</description></item>
    /// <item><description><strong>Status Aggregation</strong>: Collect child statuses for parent status determination</description></item>
    /// <item><description><strong>Diagnostic Inspection</strong>: Examine node structure for troubleshooting</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Order Independence</strong>: Array order does not reflect processing order (use Priority/Order properties)</description></item>
    /// <item><description><strong>Performance</strong>: O(n) operation where n is the number of children</description></item>
    /// <item><description><strong>Memory Usage</strong>: Creates new array on each call, consider caching for frequent access</description></item>
    /// </list>
    /// </remarks>
    ILSProcessNode[] GetChildren();
    
    /// <summary>
    /// Removes a child node from this layer node's collection.
    /// </summary>
    /// <param name="nodeID">The unique identifier of the child node to remove.</param>
    /// <returns>True if the child was found and removed, false if no child with the specified ID existed.</returns>
    /// <remarks>
    /// <para><strong>Processing Restriction:</strong> Some implementations may prevent removing children while processing is active.</para>
    /// <para><strong>Status Impact:</strong> Removing children may affect the parent node's aggregated status.</para>
    /// </remarks>
    bool RemoveChild(string nodeID);
    
    /// <summary>
    /// Creates an independent copy of this layer node and its entire child hierarchy.
    /// </summary>
    /// <returns>New layer node instance with deep-copied child tree.</returns>
    /// <remarks>
    /// <para><strong>Deep Cloning Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Recursive Copying</strong>: All child nodes are recursively cloned to create independent hierarchy</description></item>
    /// <item><description><strong>Structure Preservation</strong>: Node relationships, priorities, and conditions are maintained</description></item>
    /// <item><description><strong>Identity Independence</strong>: Cloned hierarchy operates independently from original</description></item>
    /// </list>
    /// 
    /// <para><strong>State Management:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Status Reset</strong>: Cloned nodes start with UNKNOWN status regardless of original state</description></item>
    /// <item><description><strong>Processing State</strong>: No active processing state is copied to the clone</description></item>
    /// <item><description><strong>Configuration Preservation</strong>: Node configuration (ID, priority, conditions) is copied</description></item>
    /// </list>
    /// 
    /// <para><strong>Shared Resources:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Handler Delegates</strong>: Handler functions are shared between original and clone</description></item>
    /// <item><description><strong>Execution Counters</strong>: May share execution statistics through base node references</description></item>
    /// <item><description><strong>Condition Delegates</strong>: Condition functions are shared but evaluated independently</description></item>
    /// </list>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Parallel Processing</strong>: Create independent processing trees for concurrent execution</description></item>
    /// <item><description><strong>Template Instantiation</strong>: Clone processing templates for multiple process instances</description></item>
    /// <item><description><strong>Testing</strong>: Create isolated copies for testing without affecting original hierarchy</description></item>
    /// <item><description><strong>State Rollback</strong>: Maintain clean copies for rollback scenarios</description></item>
    /// </list>
    /// </remarks>
    new ILSProcessLayerNode Clone();
}
