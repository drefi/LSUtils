using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

//internal record NodeResultKey(ILSEventNode? parent, ILSEventNode node);

/// <summary>
/// Simplified processing context that holds the event being processed and provides delegation methods for node operations.
/// Represents the execution environment for a single event processing session.
/// </summary>
/// <remarks>
/// <para><strong>Design Philosophy:</strong></para>
/// <para>LSEventProcessContext v5 follows a minimal responsibility pattern, containing only essential state:</para>
/// <list type="bullet">
/// <item><description><strong>Event</strong>: The event being processed</description></item>
/// <item><description><strong>Root Node</strong>: Entry point for processing operations</description></item>
/// <item><description><strong>Cancellation State</strong>: Thread-safe cancellation flag</description></item>
/// </list>
/// 
/// <para><strong>Delegation Pattern:</strong></para>
/// <para>All processing operations (Process, Resume, Fail, Cancel) are delegated to the root node,</para>
/// <para>which then coordinates the processing hierarchy according to each node's specific logic.</para>
/// 
/// <para><strong>Thread Safety:</strong></para>
/// <para>The cancellation state is managed using volatile fields to ensure visibility across threads.</para>
/// </remarks>
public class LSEventProcessContext {

    /// <summary>
    /// The event being processed by this context.
    /// Contains all event data and metadata needed for processing decisions.
    /// </summary>
    /// <value>The ILSEvent instance being processed. This property is read-only after construction.</value>
    public ILSEvent Event { get; }

    // NOTE: giving the capability to create a complex node structure, the system won't need to have callbacks;
    // any sort desired behaviour should be possible using nodes only.
    /// <summary>
    /// Thread-safe cancellation flag indicating whether processing has been cancelled.
    /// </summary>
    /// <value>True if processing has been cancelled, false otherwise.</value>
    /// <remarks>
    /// This field is volatile to ensure visibility across threads without requiring locks.
    /// Once set to true, it remains true for the lifetime of the context.
    /// </remarks>
    private volatile bool _isCancelled = false;

    /// <summary>
    /// The root node of the processing hierarchy.
    /// All processing operations are delegated to this node.
    /// </summary>
    /// <remarks>
    /// The root node coordinates the entire processing session and maintains the processing state.
    /// This field is internal as external code should use the delegation methods rather than direct access.
    /// </remarks>
    ILSEventNode _rootNode;

    /// <summary>
    /// Gets the current cancellation state of this processing context.
    /// </summary>
    /// <value>True if processing has been cancelled, false otherwise.</value>
    /// <remarks>
    /// This property provides thread-safe read access to the cancellation state.
    /// Nodes can check this during processing to determine if they should terminate early.
    /// </remarks>
    public bool IsCancelled => _isCancelled;
    /// <summary>
    /// Initializes a new processing context for the specified event and root node.
    /// </summary>
    /// <param name="event">The event to be processed.</param>
    /// <param name="rootNode">The root node that will coordinate processing.</param>
    /// <remarks>
    /// This constructor is internal as contexts are typically created by the event system infrastructure
    /// rather than directly by user code. The context starts in a non-cancelled state.
    /// </remarks>
    internal LSEventProcessContext(ILSEvent @event, ILSEventNode rootNode) {
        Event = @event;
        _rootNode = rootNode;
    }

    /// <summary>
    /// Initiates processing of the event through the root node hierarchy.
    /// </summary>
    /// <returns>The final processing status after the root node and its children have been processed.</returns>
    /// <remarks>
    /// <para><strong>Processing Flow:</strong></para>
    /// <list type="number">
    /// <item><description>Delegates processing to the root node</description></item>
    /// <item><description>Updates cancellation state if processing was cancelled</description></item>
    /// <item><description>Logs processing outcomes for debugging</description></item>
    /// </list>
    /// 
    /// <para><strong>Status Handling:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>CANCELLED</strong>: Sets internal cancellation flag</description></item>
    /// <item><description><strong>WAITING</strong>: Indicates processing is blocked and requires external Resume/Fail</description></item>
    /// <item><description><strong>SUCCESS/FAILURE</strong>: Terminal states indicating completion</description></item>
    /// </list>
    /// </remarks>
    internal LSEventProcessStatus Process() {
        System.Console.WriteLine($"[LSEventProcessContext] Processing root node '{_rootNode.NodeID}'...");
        var result = _rootNode.Process(this);
        if (result == LSEventProcessStatus.CANCELLED) {
            _isCancelled = true;
            System.Console.WriteLine($"[LSEventProcessContext] Root node processing was cancelled.");
        }
        if (result == LSEventProcessStatus.WAITING) {
            System.Console.WriteLine($"[LSEventProcessContext] Root node is waiting.");
        }
        return result;
    }

    /// <summary>
    /// Resumes processing from WAITING state for the specified nodes or all waiting nodes.
    /// </summary>
    /// <param name="nodes">Optional array of specific node IDs to resume. If null or empty, resumes all waiting nodes.</param>
    /// <returns>The processing status after the resume operation.</returns>
    /// <remarks>
    /// <para><strong>Delegation:</strong> This method delegates to the root node's Resume method, which then</para>
    /// <para>coordinates resumption across the node hierarchy based on the provided node IDs.</para>
    /// 
    /// <para><strong>Targeting:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Specific Nodes</strong>: When node IDs are provided, only those nodes are targeted</description></item>
    /// <item><description><strong>All Waiting</strong>: When no IDs are provided, all waiting nodes in the hierarchy are resumed</description></item>
    /// </list>
    /// </remarks>
    public LSEventProcessStatus Resume(params string[]? nodes) {
        return _rootNode.Resume(this, nodes);
    }

    /// <summary>
    /// Forces transition from WAITING to FAILURE state for the specified nodes or all waiting nodes.
    /// </summary>
    /// <param name="nodes">Optional array of specific node IDs to fail. If null or empty, fails all waiting nodes.</param>
    /// <returns>The processing status after the fail operation.</returns>
    /// <remarks>
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Timeout Handling</strong>: Force failure when operations take too long</description></item>
    /// <item><description><strong>Error Conditions</strong>: Propagate external errors to waiting nodes</description></item>
    /// <item><description><strong>Resource Constraints</strong>: Fail operations when resources are unavailable</description></item>
    /// </list>
    /// 
    /// <para><strong>Cascading Effects:</strong> Failing nodes may trigger status changes in parent nodes</para>
    /// <para>based on their aggregation logic (sequence, selector, parallel).</para>
    /// </remarks>
    public LSEventProcessStatus Fail(params string[]? nodes) {
        return _rootNode.Fail(this, nodes);
    }
    
    /// <summary>
    /// Cancels processing for the entire node hierarchy and sets the context cancellation state.
    /// </summary>
    /// <remarks>
    /// <para><strong>Scope:</strong> Cancellation affects the entire processing hierarchy rooted at the root node.</para>
    /// <para><strong>Finality:</strong> Once cancelled, the context cannot be resumed or continued.</para>
    /// <para><strong>Thread Safety:</strong> This method safely updates the cancellation state using volatile fields.</para>
    /// 
    /// <para><strong>Error Handling:</strong> If the root node's Cancel method doesn't return CANCELLED status,</para>
    /// <para>a warning is logged, but the context cancellation state is still set to ensure consistency.</para>
    /// </remarks>
    public void Cancel() {
        var result = _rootNode.Cancel(this);
        if (result != LSEventProcessStatus.CANCELLED) {
            System.Console.WriteLine($"[LSEventProcessContext] Warning: Root node Cancel() did not return CANCELLED status.");
        }
            _isCancelled = true;
            System.Console.WriteLine($"[LSEventProcessContext] Root node processing was cancelled.");
    }
}
