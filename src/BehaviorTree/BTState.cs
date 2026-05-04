namespace LSUtils.BehaviorTree;

using LSUtils.StateMachine;
/// <summary>
/// A <see cref="LSUtilState"/> that runs a <see cref="BehaviorTree"/> as its behavior logic.
/// <para>
/// On each <see cref="Update"/> tick the behavior tree instance is advanced by one frame.
/// When the tree returns <see cref="BTStatus.SUCCESS"/> or <see cref="BTStatus.FAILURE"/>,
/// a named event is dispatched to the parent <see cref="LSUtilHSM"/> so it can
/// transition to the next state. Event names are configurable via
/// <see cref="SuccessEvent"/> and <see cref="FailureEvent"/>.
/// </para>
/// <para>
/// The tree is aborted and reset to <see cref="BTStatus.FRESH"/> each time this state
/// is re-entered, so consecutive activations always start from scratch.
/// </para>
/// </summary>
public class BTState : LSUtilState {
    BehaviorTree _behaviorTree;
    readonly Blackboard? _sharedBlackboard;
    BTInstance? _instance;

    /// <summary>
    /// BehaviorTree definition used by this state.
    /// </summary>
    public BehaviorTree BehaviorTree {
        get => _behaviorTree;
        set => _behaviorTree = value;
    }

    /// <summary>
    /// Event name dispatched to the parent HSM when the behavior tree returns SUCCESS.
    /// Defaults to <c>"success"</c>.
    /// </summary>
    public string SuccessEvent { get; set; } = "success";

    /// <summary>
    /// Event name dispatched to the parent HSM when the behavior tree returns FAILURE.
    /// Defaults to <c>"failure"</c>.
    /// </summary>
    public string FailureEvent { get; set; } = "failure";

    /// <summary>
    /// Enables optional performance monitoring hooks for this state.
    /// The core runtime currently does not add overhead when this is false.
    /// </summary>
    public bool MonitorPerformance { get; set; }

    /// <summary>
    /// The underlying <see cref="BTInstance"/> that drives the tree.
    /// Available after <see cref="Setup"/> has been called; <c>null</c> before that.
    /// </summary>
    public BTInstance? Player => _instance;

    /// <summary>
    /// Returns the runtime behavior tree instance created for this state.
    /// </summary>
    public BTInstance? GetBTInstance() => _instance;

    /// <param name="name">State name used for transitions and debugging.</param>
    /// <param name="behaviorTree">BehaviorTree definition to run.</param>
    /// <param name="blackboard">
    /// Optional shared <see cref="Blackboard"/>. A new root-scope blackboard is created
    /// automatically when <c>null</c>. Pass a shared instance when multiple states need
    /// to exchange data through the blackboard.
    /// </param>
    public BTState(string name, BehaviorTree behaviorTree, Blackboard? blackboard = null) : base(name) {
        _behaviorTree = behaviorTree;
        _sharedBlackboard = blackboard;
    }

    /// <summary>
    /// Convenience overload that wraps a root task into a <see cref="BehaviorTree"/>.
    /// </summary>
    public BTState(string name, BTTask treeRoot, Blackboard? blackboard = null) : this(
        name,
        new BehaviorTree(treeRoot),
        blackboard) { }

    /// <summary>Creates the <see cref="BTInstance"/> and initializes it against the agent.</summary>
    protected override void OnSetup(object? agent) {
        _instance = _behaviorTree.Instantiate(agent, _sharedBlackboard, this);
    }

    /// <summary>Aborts the behavior tree so it restarts fresh on the next <see cref="Update"/>.</summary>
    protected override void OnEnter() {
        _instance?.Abort();
    }

    /// <summary>
    /// Advances the behavior tree by one frame.
    /// Dispatches <see cref="SuccessEvent"/> or <see cref="FailureEvent"/> to the parent
    /// HSM when the tree reaches a terminal status.
    /// </summary>
    protected override void OnUpdate(float delta) {
        if (_instance is null) {
            return;
        }

        var status = _instance.Update(delta);

        if (status == BTStatus.SUCCESS) {
            Hsm?.DispatchEvent(SuccessEvent);
        } else if (status == BTStatus.FAILURE) {
            Hsm?.DispatchEvent(FailureEvent);
        }
    }
}
