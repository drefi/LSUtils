namespace LSUtils.BehaviorTree;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Stores default key/value entries used to construct fresh <see cref="Blackboard"/> instances.
/// <para>
/// This is a lightweight replacement for Godot/LimboAI's BlackboardPlan concept.
/// In LSUtils, the plan's purpose is simply to seed predictable defaults into a
/// new blackboard before a tree instance starts running.
/// </para>
/// </summary>
public sealed class BlackboardPlan {
    readonly Dictionary<string, object?> _defaults = new(StringComparer.Ordinal);

    /// <summary>Returns a read-only view of the default entries in this plan.</summary>
    public IReadOnlyDictionary<string, object?> Defaults => _defaults;

    /// <summary>Sets or replaces a default value for a key.</summary>
    public void SetDefault<T>(string key, [AllowNull] T value) {
        _defaults[key] = value;
    }

    /// <summary>Removes a default value entry.</summary>
    public bool RemoveDefault(string key) => _defaults.Remove(key);

    /// <summary>Clears all default entries in the plan.</summary>
    public void Clear() => _defaults.Clear();

    /// <summary>
    /// Creates a new blackboard and applies all plan defaults to it.
    /// </summary>
    /// <param name="parent">Optional parent blackboard for scope chaining.</param>
    public Blackboard CreateBlackboard(Blackboard? parent = null) {
        var blackboard = new Blackboard(parent);
        foreach (var pair in _defaults) {
            blackboard.Set(pair.Key, pair.Value);
        }
        return blackboard;
    }

    /// <summary>Copies all defaults from another plan into this plan, replacing existing entries.</summary>
    public void CopyOther(BlackboardPlan other) {
        ArgumentNullException.ThrowIfNull(other);

        _defaults.Clear();
        foreach (var pair in other._defaults) {
            _defaults[pair.Key] = pair.Value;
        }
    }

    /// <summary>Creates a copy of this plan.</summary>
    public BlackboardPlan Clone() {
        var plan = new BlackboardPlan();
        plan.CopyOther(this);
        return plan;
    }
}
