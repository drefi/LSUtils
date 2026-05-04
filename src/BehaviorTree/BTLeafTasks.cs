namespace LSUtils.BehaviorTree;

using System;

/// <summary>
/// Base class for action leaf tasks. Extend this to implement your own actions.
/// </summary>
public abstract class BTAction : BTTask {
    protected BTAction(string name) : base(name) { }
}

/// <summary>
/// Base class for condition leaf tasks.
/// <see cref="BTTask.Tick"/> is sealed — implement <see cref="Check"/> instead.
/// Returns SUCCESS when <see cref="Check"/> is true, FAILURE otherwise.
/// </summary>
public abstract class BTCondition : BTTask {
    protected BTCondition(string name) : base(name) { }

    protected sealed override BTStatus Tick(float delta) => Check() ? BTStatus.SUCCESS : BTStatus.FAILURE;

    /// <summary>Evaluate the condition. Called once per tick.</summary>
    protected abstract bool Check();
}

/// <summary>
/// Inline action built from a delegate.
/// The delegate receives the task itself (for name/path), the shared blackboard, and the frame delta.
/// </summary>
public sealed class DelegateBTAction : BTAction {
    readonly Func<BTTask, Blackboard, float, BTStatus> _action;

    public DelegateBTAction(string name, Func<BTTask, Blackboard, float, BTStatus> action) : base(name) {
        _action = action;
    }

    protected override BTStatus Tick(float delta) => _action(this, Blackboard, delta);

    public override BTTask CloneTask() => new DelegateBTAction(Name, _action) {
        CustomName = CustomName,
    };
}

/// <summary>
/// Inline condition built from a predicate delegate.
/// </summary>
public sealed class DelegateBTCondition : BTCondition {
    readonly Func<Blackboard, bool> _predicate;

    public DelegateBTCondition(string name, Func<Blackboard, bool> predicate) : base(name) {
        _predicate = predicate;
    }

    protected override bool Check() => _predicate(Blackboard);

    public override BTTask CloneTask() => new DelegateBTCondition(Name, _predicate) {
        CustomName = CustomName,
    };
}
