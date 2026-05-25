namespace LSUtils.BehaviorTree;

using System;

/// <summary>
/// Base class for all BehaviorTree tasks.
/// <para>
/// Tasks perform their work by implementing <see cref="Tick"/>. Use
/// <see cref="Enter"/> for per-activation setup and <see cref="Exit"/> for
/// cleanup. Use <see cref="Setup"/> for one-time initialization.
/// </para>
/// <para>
/// Do not extend <see cref="BTTask"/> directly. Extend one of
/// <see cref="BTAction"/>, <see cref="BTCondition"/>,
/// <see cref="BTComposite"/>, or <see cref="BTDecorator"/>.
/// </para>
/// </summary>
public abstract class BTTask {
    bool _initialized;
    Blackboard? _blackboard;

    /// <summary>Node name for debugging and path resolution.</summary>
    public string Name { get; }

    /// <summary>Optional display name that overrides <see cref="Name"/> in debug output.</summary>
    public string CustomName { get; set; } = "";

    /// <summary>Parent task. Set automatically by composite and decorator tasks.</summary>
    public BTTask? Parent { get; internal set; }

    /// <summary>Full path from root (e.g. "Root/Combat/UseSkill").</summary>
    public string Path => Parent is null ? Name : $"{Parent.Path}/{Name}";

    /// <summary>Current execution status. Starts as <see cref="BTStatus.FRESH"/>.</summary>
    public BTStatus TaskStatus { get; private set; } = BTStatus.FRESH;

    /// <summary>Accumulated time in seconds since this task last entered RUNNING.</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>
    /// The agent this task belongs to. Assigned during <see cref="Initialize"/>.
    /// Typically the object that owns the behavior tree (e.g. the AI controller).
    /// </summary>
    public object? Agent { get; private set; }

    /// <summary>Shared blackboard. Valid after <see cref="Initialize"/> is called.</summary>
    protected Blackboard Blackboard => _blackboard
        ?? throw new InvalidOperationException("Task has not been initialized. Call Initialize before Execute.");

    protected BTTask(string name) {
        Name = name;
    }

    /// <summary>
    /// Initializes the task: assigns <paramref name="agent"/> and <paramref name="blackboard"/>,
    /// then calls <see cref="Setup"/>. Composites and decorators override this to recurse
    /// into their children so the entire tree is initialized in one call.
    /// </summary>
    /// <param name="agent">The contextual object that owns this behavior tree.</param>
    /// <param name="blackboard">The shared blackboard for this tree.</param>
    /// <param name="sceneRoot">
    /// Optional root context object (e.g. the scene or world root).
    /// Forwarded to child tasks unchanged.
    /// </param>
    public virtual void Initialize(object? agent, Blackboard blackboard, object? sceneRoot = null) {
        Agent = agent;
        _blackboard = blackboard;
        Setup();
        _initialized = true;
    }

    /// <summary>
    /// Runs the task lifecycle for one frame.
    /// <list type="number">
    ///   <item>Calls <see cref="Enter"/> if the previous status was not RUNNING.</item>
    ///   <item>Calls <see cref="Tick"/> to get the new status.</item>
    ///   <item>Calls <see cref="Exit"/> if the new status is SUCCESS or FAILURE.</item>
    /// </list>
    /// </summary>
    public BTStatus Execute(float delta) {
        if (!_initialized)
            throw new InvalidOperationException("Call Initialize before Execute.");

        if (TaskStatus != BTStatus.RUNNING) {
            Enter();
            ElapsedTime = 0f;
        }

        ElapsedTime += delta;
        var result = Tick(delta);
        TaskStatus = result;

        if (result != BTStatus.RUNNING) {
            Exit();
        }

        return result;
    }

    /// <summary>
    /// Resets this task (and children, if any) to <see cref="BTStatus.FRESH"/>.
    /// Calls <see cref="Exit"/> if the task is currently RUNNING.
    /// </summary>
    public virtual void Abort() {
        if (TaskStatus == BTStatus.RUNNING) {
            Exit();
        }
        TaskStatus = BTStatus.FRESH;
        ElapsedTime = 0f;
    }

    /// <summary>Called once inside <see cref="Initialize"/>. Override for one-time initialization.</summary>
    protected virtual void Setup() { }

    /// <summary>Called before <see cref="Tick"/> when the previous status was not RUNNING.</summary>
    protected virtual void Enter() { }

    /// <summary>Called after <see cref="Tick"/> returns SUCCESS or FAILURE.</summary>
    protected virtual void Exit() { }

    protected abstract BTStatus Tick(float delta);

    /// <summary>
    /// Creates a structural copy of this task node.
    /// <para>
    /// Built-in LSUtils tasks override this to support runtime tree instantiation.
    /// Custom tasks should override it when they need to be used with
    /// <see cref="BehaviorTree.Instantiate"/> in multi-instance scenarios.
    /// </para>
    /// </summary>
    public virtual BTTask CloneTask() {
        throw new NotSupportedException(
            $"Task type '{GetType().Name}' does not implement CloneTask. " +
            "Override CloneTask() in custom BTTask types.");
    }
}
