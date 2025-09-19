namespace LSUtils.EventSystem;

/// <summary>
/// Extended interface for composite nodes that can contain and manage child nodes in the event processing hierarchy.
/// Implements the Composite Pattern by extending ILSEventNode with child management capabilities.
/// </summary>
/// <remarks>
/// <para>This interface is implemented by:</para>
/// <list type="bullet">
/// <item><description><strong>LSEventSequenceNode</strong>: Processes children sequentially until one fails (AND logic)</description></item>
/// <item><description><strong>LSEventSelectorNode</strong>: Processes children sequentially until one succeeds (OR logic)</description></item>
/// <item><description><strong>LSEventParallelNode</strong>: Processes children simultaneously with threshold-based completion</description></item>
/// </list>
/// 
/// <para><strong>Child Management:</strong></para>
/// <list type="bullet">
/// <item><description>Children are stored in a dictionary keyed by NodeID for O(1) lookup</description></item>
/// <item><description>Child processing order is determined by Priority (descending) then Order (ascending)</description></item>
/// <item><description>Condition evaluation determines which children are eligible for processing</description></item>
/// </list>
/// 
/// <para><strong>Processing Patterns:</strong></para>
/// <list type="bullet">
/// <item><description>All layer nodes follow a unified processing pattern with _availableChildren and _processStack</description></item>
/// <item><description>Status aggregation logic varies by implementation (sequence vs selector vs parallel)</description></item>
/// <item><description>Resume/Fail operations delegate to appropriate waiting child nodes</description></item>
/// </list>
/// </remarks>
public interface ILSEventLayerNode : ILSEventNode {
    /// <summary>
    /// Adds a child node to this layer node's collection.
    /// The child will be processed according to this node's processing logic.
    /// </summary>
    /// <param name="child">The child node to add. Must have a unique NodeID within this parent.</param>
    /// <remarks>
    /// <para><strong>Uniqueness:</strong> If a child with the same NodeID already exists, it will be replaced.</para>
    /// <para><strong>Processing Restriction:</strong> Some implementations may prevent adding children while processing is active.</para>
    /// <para><strong>Ordering:</strong> Children are processed based on Priority (descending) then Order (ascending).</para>
    /// </remarks>
    void AddChild(ILSEventNode child);
    
    /// <summary>
    /// Retrieves a child node by its unique identifier.
    /// </summary>
    /// <param name="nodeID">The unique identifier of the child node to retrieve.</param>
    /// <returns>The child node with the specified ID, or null if not found.</returns>
    /// <remarks>This operation is O(1) due to dictionary-based storage.</remarks>
    ILSEventNode? GetChild(string nodeID);
    
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
    /// <para><strong>Snapshot:</strong> Returns a snapshot of children at the time of call.</para>
    /// <para><strong>Processing Order:</strong> Actual processing order depends on Priority and Order properties, not array position.</para>
    /// <para><strong>Immutability:</strong> Modifying the returned array does not affect the parent's child collection.</para>
    /// </remarks>
    ILSEventNode[] GetChildren(); // Exposes children for navigation and cloning
    
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
    /// <para><strong>Deep Copy:</strong> All child nodes are recursively cloned to create an independent hierarchy.</para>
    /// <para><strong>State Reset:</strong> Cloned nodes typically start with UNKNOWN status regardless of original state.</para>
    /// <para><strong>Shared Resources:</strong> Handler nodes may share execution counts with their originals through base node references.</para>
    /// </remarks>
    new ILSEventLayerNode Clone();
}
