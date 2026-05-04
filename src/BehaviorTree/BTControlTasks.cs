namespace LSUtils.BehaviorTree;

using System.Collections.Generic;

/// <summary>
/// Base for composite (multi-child) control tasks.
/// Composites propagate <see cref="BTTask.Abort"/> recursively to all children
/// and override <see cref="BTTask.Initialize"/> to initialize each child in turn.
/// </summary>
public abstract class BTComposite : BTTask {
    readonly List<BTTask> _children;

    protected BTComposite(string name, params BTTask[] children) : base(name) {
        _children = new List<BTTask>(children);
        foreach (var child in _children) {
            child.Parent = this;
        }
    }

    protected IReadOnlyList<BTTask> Children => _children;

    /// <summary>Adds a child at the end of the children list.</summary>
    public void AddChild(BTTask child) {
        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>Adds a child at a specific index in the children list.</summary>
    public void AddChildAt(BTTask child, int index) {
        child.Parent = this;
        _children.Insert(index, child);
    }

    /// <inheritdoc/>
    public override void Initialize(object? agent, Blackboard blackboard, object? sceneRoot = null) {
        base.Initialize(agent, blackboard, sceneRoot);
        foreach (var child in _children) {
            child.Initialize(agent, blackboard, sceneRoot);
        }
    }

    public override void Abort() {
        foreach (var child in _children) {
            child.Abort();
        }
        base.Abort();
    }

    protected BTTask[] CloneChildren() {
        var cloned = new BTTask[_children.Count];
        for (int i = 0; i < _children.Count; i++) {
            cloned[i] = _children[i].CloneTask();
        }
        return cloned;
    }
}

/// <summary>
/// Priority selector (OR logic): ticks children in order and returns
/// the first non-<see cref="BTStatus.FAILURE"/> result. Returns FAILURE
/// only if every child fails.
/// </summary>
public sealed class BTSelector : BTComposite {
    public BTSelector(string name, params BTTask[] children) : base(name, children) { }

    protected override BTStatus Tick(float delta) {
        foreach (var child in Children) {
            var status = child.Execute(delta);
            if (status != BTStatus.FAILURE) {
                return status;
            }
        }

        return BTStatus.FAILURE;
    }

    public override BTTask CloneTask() => new BTSelector(Name, CloneChildren()) {
        CustomName = CustomName,
    };
}

/// <summary>
/// Sequence (AND logic): ticks children in order and returns
/// the first non-<see cref="BTStatus.SUCCESS"/> result. Returns SUCCESS
/// only if every child succeeds.
/// </summary>
public sealed class BTSequence : BTComposite {
    public BTSequence(string name, params BTTask[] children) : base(name, children) { }

    protected override BTStatus Tick(float delta) {
        foreach (var child in Children) {
            var status = child.Execute(delta);
            if (status != BTStatus.SUCCESS) {
                return status;
            }
        }

        return BTStatus.SUCCESS;
    }

    public override BTTask CloneTask() => new BTSequence(Name, CloneChildren()) {
        CustomName = CustomName,
    };
}

/// <summary>
/// Base for single-child decorator tasks.
/// </summary>
public abstract class BTDecorator : BTTask {
    protected readonly BTTask Child;

    protected BTDecorator(string name, BTTask child) : base(name) {
        Child = child;
        child.Parent = this;
    }

    /// <inheritdoc/>
    public override void Initialize(object? agent, Blackboard blackboard, object? sceneRoot = null) {
        base.Initialize(agent, blackboard, sceneRoot);
        Child.Initialize(agent, blackboard, sceneRoot);
    }

    public override void Abort() {
        Child.Abort();
        base.Abort();
    }
}

/// <summary>
/// Decorator that flips SUCCESS to FAILURE and vice versa.
/// RUNNING passes through unchanged.
/// </summary>
public sealed class BTInvert : BTDecorator {
    public BTInvert(string name, BTTask child) : base(name, child) { }

    protected override BTStatus Tick(float delta) {
        return Child.Execute(delta) switch {
            BTStatus.SUCCESS => BTStatus.FAILURE,
            BTStatus.FAILURE => BTStatus.SUCCESS,
            var other => other,
        };
    }

    public override BTTask CloneTask() => new BTInvert(Name, Child.CloneTask()) {
        CustomName = CustomName,
    };
}

/// <summary>
/// Decorator that always returns SUCCESS regardless of child outcome.
/// Useful for optional steps in a <see cref="BTSequence"/>.
/// </summary>
public sealed class BTAlwaysSucceed : BTDecorator {
    public BTAlwaysSucceed(string name, BTTask child) : base(name, child) { }

    protected override BTStatus Tick(float delta) {
        Child.Execute(delta);
        return BTStatus.SUCCESS;
    }

    public override BTTask CloneTask() => new BTAlwaysSucceed(Name, Child.CloneTask()) {
        CustomName = CustomName,
    };
}
