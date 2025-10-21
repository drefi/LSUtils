namespace LSUtils.ProcessSystem;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;

/// <summary>
/// Simplified processing context that holds the process being executed and provides delegation methods for node operations.
/// Represents the execution environment for a single processing session within the LSProcessing system.
/// </summary>
/// <remarks>
/// <b>Design Philosophy:</b><br/>
/// LSProcessSession follows a minimal responsibility pattern, containing only essential state:<br/>
/// • <b>Process:</b> The process being executed<br/>
/// • <b>Root Node:</b> Entry point for processing operations<br/>
/// • <b>Cancellation State:</b> Thread-safe cancellation flag<br/>
/// <br/>
/// <b>Delegation Pattern:</b><br/>
/// All processing operations (Execute, Resume, Fail, Cancel) are delegated to the root node, which then coordinates the processing hierarchy according to each node's specific logic.<br/>
/// <br/>
/// <b>Thread Safety:</b><br/>
/// The cancellation state is managed using volatile fields to ensure visibility across threads without requiring locks.
/// </remarks>
public class LSProcessSession {
    public const string ClassName = nameof(LSProcessSession);

    /// <summary>
    /// The root node of the processing hierarchy.
    /// All processing operations are delegated to this node.
    /// </summary>
    /// <remarks>
    /// <b>Coordination Role:</b><br/>
    /// The root node coordinates the entire processing session and maintains the processing state for the complete node hierarchy.<br/>
    /// <br/>
    /// <b>Access Control:</b><br/>
    /// This field is internal as external code should use the delegation methods (Execute, Resume, Fail, Cancel) rather than direct node access.
    /// </remarks>
    public ILSProcessNode RootNode { get; }

    public System.Guid SessionID { get; } = System.Guid.NewGuid();

    /// <summary>
    /// The process being executed by this session.
    /// Contains all process data and metadata needed for processing decisions.
    /// </summary>
    /// <value>The ILSProcess instance being executed. This property is read-only after construction.</value>
    /// <remarks>
    /// <b>Process Access:</b><br/>
    /// Nodes can access process data through this property during Execute, Resume, Fail, and Cancel operations. The process instance provides context for condition evaluation and processing logic.
    /// </remarks>
    public LSProcessManager Manager { get; }
    public ILSProcess Process { get; }
    public ILSProcessable? Instance { get; }
    internal Stack<ILSProcessNode> _sessionStack = new Stack<ILSProcessNode>();
    public ILSProcessNode? CurrentNode => _sessionStack.Count > 0 ? _sessionStack.Peek() : null;

    /// <summary>
    /// Initializes a new processing session for the specified process and root node.
    /// </summary>
    /// <param name="process">The process to be executed.</param>
    /// <param name="rootNode">The root node that will coordinate processing.</param>
    /// <remarks>
    /// <b>Infrastructure Creation:</b><br/>
    /// This constructor is internal as sessions are typically created by the processing system infrastructure rather than directly by user code.<br/>
    /// <br/>
    /// <b>Initial State:</b><br/>
    /// The session starts in a non-cancelled state with the provided process and root node ready for execution.
    /// </remarks>
    internal LSProcessSession(LSProcessManager manager, ILSProcess process, ILSProcessNode rootNode, ILSProcessable? instance = null) {
        Manager = manager;
        Process = process;
        RootNode = rootNode;
        Instance = instance;
    }

    /// <summary>
    /// Initiates processing of the process through the root node hierarchy.
    /// </summary>
    /// <returns>The final processing status after the root node and its children have been processed.</returns>
    /// <remarks>
    /// <b>Processing Flow:</b><br/>
    /// 1. Delegates processing to the root node<br/>
    /// 2. Updates cancellation state if processing was cancelled<br/>
    /// 3. Logs processing outcomes for debugging<br/>
    /// <br/>
    /// <b>Status Handling:</b><br/>
    /// • <b>CANCELLED:</b> Sets internal cancellation flag<br/>
    /// • <b>WAITING:</b> Indicates processing is blocked and requires external Resume/Fail<br/>
    /// • <b>SUCCESS/FAILURE:</b> Terminal states indicating completion
    /// </remarks>
    internal LSProcessResultStatus Execute() {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Execute RootNode: [{RootNode.NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));

        var rootStatus = RootNode.GetNodeStatus();
        if (rootStatus != LSProcessResultStatus.UNKNOWN && rootStatus != LSProcessResultStatus.WAITING)
            return rootStatus;
        LSLogger.Singleton.Debug($"Session Execute",
              source: (ClassName, null),
              processId: Process.ID,
              properties: new (string, object)[] {
                ("sessionID", SessionID),
                ("rootNodeID", RootNode.NodeID),
                ("method", nameof(Execute))
            });
        _sessionStack.Push(RootNode);
        var result = RootNode.Execute(this);
        _sessionStack.Pop();
        return result;
    }

    /// <summary>
    /// Resumes processing from WAITING state for the specified nodes or all waiting nodes.
    /// </summary>
    /// <param name="nodes">Optional array of specific node IDs to resume. If null or empty, resumes all waiting nodes.</param>
    /// <returns>The processing status after the resume operation.</returns>
    /// <remarks>
    /// <b>Delegation:</b><br/>
    /// This method delegates to the root node's Resume method, which then coordinates resumption across the node hierarchy based on the provided node IDs.<br/>
    /// <br/>
    /// <b>Targeting:</b><br/>
    /// • <b>Specific Nodes:</b> When node IDs are provided, only those nodes are targeted<br/>
    /// • <b>All Waiting:</b> When no IDs are provided, all waiting nodes in the hierarchy are resumed
    /// </remarks>
    public LSProcessResultStatus Resume(params string[]? nodes) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Resume",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        if (RootNode == null) {
            //log warning
            LSLogger.Singleton.Warning($"Session does not have root node.",
                source: (ClassName, null),
                processId: Process.ID,
                properties: new (string, object)[] {
                    ("sessionID", SessionID),
                    ("method", nameof(Resume))
                });
            return LSProcessResultStatus.UNKNOWN;
        }
        return RootNode.Resume(this, nodes);
    }

    /// <summary>
    /// Forces transition from WAITING to FAILURE state for the specified nodes or all waiting nodes.
    /// </summary>
    /// <param name="nodes">Optional array of specific node IDs to fail. If null or empty, fails all waiting nodes.</param>
    /// <returns>The processing status after the fail operation.</returns>
    /// <remarks>
    /// <b>Use Cases:</b><br/>
    /// • <b>Timeout Handling:</b> Force failure when operations take too long<br/>
    /// • <b>Error Conditions:</b> Propagate external errors to waiting nodes<br/>
    /// • <b>Resource Constraints:</b> Fail operations when resources are unavailable<br/>
    /// <br/>
    /// <b>Cascading Effects:</b><br/>
    /// Failing nodes may trigger status changes in parent nodes based on their aggregation logic (sequence, selector, parallel).
    /// </remarks>
    public LSProcessResultStatus Fail(params string[]? nodes) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Fail",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));

        if (RootNode == null) {
            //log warning
            LSLogger.Singleton.Warning($"Session does not have root node.",
                source: (ClassName, null),
                processId: Process.ID,
                properties: new (string, object)[] {
                    ("sessionID", SessionID),
                    ("method", nameof(Fail))
                });
            return LSProcessResultStatus.UNKNOWN;
        }
        return RootNode.Fail(this, nodes);
    }

    /// <summary>
    /// Cancels processing for the entire node hierarchy and sets the session cancellation state.
    /// </summary>
    /// <remarks>
    /// <b>Scope:</b><br/>
    /// Cancellation affects the entire processing hierarchy rooted at the root node.<br/>
    /// <br/>
    /// <b>Finality:</b><br/>
    /// Once cancelled, the session cannot be resumed or continued.<br/>
    /// <br/>
    /// <b>Thread Safety:</b><br/>
    /// This method safely updates the cancellation state using volatile fields.<br/>
    /// <br/>
    /// <b>Error Handling:</b><br/>
    /// If the root node's Cancel method doesn't return CANCELLED status, a warning is logged, but the session cancellation state is still set to ensure consistency.
    /// </remarks>
    public LSProcessResultStatus Cancel() {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Cancel",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));

        if (RootNode == null) {
            //log warning
            LSLogger.Singleton.Warning($"Session does not have root node.",
                source: (ClassName, null),
                processId: Process.ID,
                properties: new (string, object)[] {
                        ("sessionID", SessionID),
                        ("method", nameof(Cancel))
                });
            return LSProcessResultStatus.UNKNOWN;
        }
        var result = RootNode.Cancel(this);
        if (result != LSProcessResultStatus.CANCELLED) {
            LSLogger.Singleton.Warning($"Root node Cancel did not return CANCELLED status.",
                  source: (ClassName, null),
                  processId: Process.ID,
                  properties: new (string, object)[] {
                    ("session", SessionID),
                    ("rootNode", RootNode.NodeID),
                    ("currentNode", CurrentNode?.NodeID ?? "null"),
                    ("result", result.ToString()),
                    ("method", nameof(Cancel))
                });
        }
        return result;
    }
}
