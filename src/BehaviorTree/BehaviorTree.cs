namespace LSUtils.BehaviorTree;

using System;

/// <summary>
/// Contains behavior tree definition data and can instantiate runtime tree instances.
/// <para>
/// A behavior tree is a hierarchical decision structure made from <see cref="BTTask"/>
/// nodes. Execution starts from the root task and progresses according to control tasks
/// such as <see cref="BTSequence"/>, <see cref="BTSelector"/>, and <see cref="BTInvert"/>.
/// Leaf tasks represent concrete actions and conditions.
/// </para>
/// </summary>
public sealed class BehaviorTree {
    BTTask? _rootTask;

    /// <summary>
    /// Optional plan used to seed a fresh blackboard during instantiation.
    /// </summary>
    public BlackboardPlan BlackboardPlan { get; set; } = new();

    /// <summary>
    /// Optional human-readable description for tooling or diagnostics.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Creates an empty behavior tree definition.</summary>
    public BehaviorTree() { }

    /// <summary>Creates a behavior tree definition with an initial root task.</summary>
    public BehaviorTree(BTTask rootTask) {
        SetRootTask(rootTask);
    }

    /// <summary>Sets the root task for this definition.</summary>
    public void SetRootTask(BTTask task) {
        ArgumentNullException.ThrowIfNull(task);
        task.Parent = null;
        _rootTask = task;
    }

    /// <summary>Returns the root task for this definition, or <c>null</c> if not set.</summary>
    public BTTask? GetRootTask() => _rootTask;

    /// <summary>
    /// Copies definition metadata and root task reference from another definition.
    /// <para>
    /// The root task is cloned via <see cref="BTTask.CloneTask"/> so each definition
    /// owns an isolated task graph.
    /// </para>
    /// </summary>
    public void CopyOther(BehaviorTree other) {
        ArgumentNullException.ThrowIfNull(other);

        _rootTask = other._rootTask?.CloneTask();
        if (_rootTask is not null) {
            _rootTask.Parent = null;
        }
        Description = other.Description;
        BlackboardPlan = other.BlackboardPlan.Clone();
    }

    /// <summary>
    /// Creates a copy of this definition.
    /// <para>
    /// The root task and <see cref="BlackboardPlan"/> are both cloned.
    /// </para>
    /// </summary>
    public BehaviorTree Clone() {
        var copy = new BehaviorTree();
        copy.CopyOther(this);
        return copy;
    }

    /// <summary>
    /// Creates a runtime instance of this behavior tree.
    /// </summary>
    /// <param name="agent">Agent context exposed to tasks via <see cref="BTTask.Agent"/>.</param>
    /// <param name="blackboard">
    /// Optional blackboard. If null, a new one is created from <see cref="BlackboardPlan"/>.
    /// </param>
    /// <param name="instanceOwner">
    /// Optional owner object associated with the instance.
    /// </param>
    /// <param name="customSceneRoot">
    /// Optional scene/world root context forwarded to <see cref="BTTask.Initialize"/>.
    /// </param>
    public BTInstance Instantiate(
        object? agent,
        Blackboard? blackboard,
        object? instanceOwner,
        object? customSceneRoot = null) {
        if (_rootTask is null) {
            throw new InvalidOperationException("BehaviorTree has no root task. Call SetRootTask first.");
        }

        var resolvedBlackboard = blackboard ?? BlackboardPlan.CreateBlackboard();
        var root = _rootTask.CloneTask();
        root.Parent = null;
        var player = new BTPlayer(root, agent, resolvedBlackboard, customSceneRoot);
        return new BTInstance(this, player, instanceOwner);
    }
}
