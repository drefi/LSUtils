namespace LSUtils.BehaviorTree;

/// <summary>
/// Runtime host for a behavior tree. Initializes the tree against an agent and
/// blackboard, then drives it each frame via <see cref="Update"/>.
/// </summary>
/// <remarks>
/// Equivalent to LimboAI's <c>BTPlayer</c> node, adapted for non-Godot use.
/// The agent is the contextual owner of the tree (e.g. an AI controller).
/// The blackboard is shared across the entire tree for the lifetime of this player.
/// </remarks>
public sealed class BTPlayer {
    readonly BTTask _root;
    readonly Blackboard _blackboard;

    /// <summary>The agent this tree belongs to.</summary>
    public object? Agent { get; }

    /// <summary>The blackboard shared across all tasks in this tree.</summary>
    public Blackboard Blackboard => _blackboard;

    /// <summary>The root task of the behavior tree.</summary>
    public BTTask Root => _root;

    /// <summary>Current execution status after the last <see cref="Update"/> call.</summary>
    public BTStatus LastStatus { get; private set; } = BTStatus.FRESH;

    /// <param name="root">Root task of the behavior tree.</param>
    /// <param name="agent">
    /// The contextual object that owns this tree. Passed to every task via
    /// <see cref="BTTask.Agent"/>. May be <c>null</c>.
    /// </param>
    /// <param name="blackboard">
    /// Shared blackboard. A new root-scope <see cref="Blackboard"/> is created
    /// automatically when <c>null</c>.
    /// </param>
    /// <param name="sceneRoot">
    /// Optional context root (e.g. a scene or world object). Forwarded to
    /// <see cref="BTTask.Initialize"/> unchanged.
    /// </param>
    public BTPlayer(BTTask root, object? agent = null, Blackboard? blackboard = null, object? sceneRoot = null) {
        _root = root;
        _blackboard = blackboard ?? new Blackboard();
        Agent = agent;
        _root.Initialize(agent, _blackboard, sceneRoot);
    }

    /// <summary>Ticks the behavior tree for one frame and returns the resulting status.</summary>
    public BTStatus Update(float delta) {
        LastStatus = _root.Execute(delta);
        return LastStatus;
    }

    /// <summary>Aborts the tree, resetting all tasks to FRESH.</summary>
    public void Abort() => _root.Abort();
}
