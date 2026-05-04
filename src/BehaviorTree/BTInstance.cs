namespace LSUtils.BehaviorTree;

/// <summary>
/// Runtime instance of a <see cref="BehaviorTree"/> definition.
/// Wraps the underlying <see cref="BTPlayer"/> and exposes a small execution API.
/// </summary>
public sealed class BTInstance {
    readonly BTPlayer _player;

    internal BTInstance(BehaviorTree definition, BTPlayer player, object? owner) {
        Definition = definition;
        _player = player;
        Owner = owner;
    }

    /// <summary>The tree definition that created this runtime instance.</summary>
    public BehaviorTree Definition { get; }

    /// <summary>Arbitrary owner object associated with this instance.</summary>
    public object? Owner { get; }

    /// <summary>Agent provided when the instance was created.</summary>
    public object? Agent => _player.Agent;

    /// <summary>Runtime blackboard used by this instance.</summary>
    public Blackboard Blackboard => _player.Blackboard;

    /// <summary>Root task of this instance.</summary>
    public BTTask Root => _player.Root;

    /// <summary>Last status produced by <see cref="Update"/>.</summary>
    public BTStatus LastStatus => _player.LastStatus;

    /// <summary>Updates the behavior tree for one frame.</summary>
    public BTStatus Update(float delta) => _player.Update(delta);

    /// <summary>Aborts the behavior tree, resetting it to FRESH.</summary>
    public void Abort() => _player.Abort();
}
