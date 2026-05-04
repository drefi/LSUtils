namespace LSUtils.BehaviorTree;

using System;
using System.Collections.Generic;

/// <summary>
/// Key/value storage for sharing data among BehaviorTree tasks.
/// Supports a parent scope chain: if a key is not found in this blackboard,
/// the lookup walks up through parent scopes. Writes always stay local.
/// </summary>
public sealed class Blackboard {
    readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>The parent scope, or <c>null</c> if this is the root scope.</summary>
    public Blackboard? Parent { get; }

    public Blackboard(Blackboard? parent = null) {
        Parent = parent;
    }

    /// <summary>Write a value into this scope only.</summary>
    public void Set<T>(string key, T value) {
        _values[key] = value;
    }

    /// <summary>
    /// Read a value, walking up the scope chain if not found locally.
    /// Returns <c>true</c> when found.
    /// </summary>
    public bool TryGet<T>(string key, out T value) {
        var scope = this;
        while (scope is not null) {
            if (scope._values.TryGetValue(key, out var boxed) && boxed is T typed) {
                value = typed;
                return true;
            }
            scope = scope.Parent;
        }
        value = default!;
        return false;
    }

    /// <summary>Read a value from the scope chain, returning <paramref name="fallback"/> if absent.</summary>
    public T GetOrDefault<T>(string key, T fallback = default!) =>
        TryGet<T>(key, out var value) ? value : fallback;

    /// <summary>Returns <c>true</c> when the key exists in this scope or any parent scope.</summary>
    public bool Contains(string key) => TryGet<object?>(key, out _);

    /// <summary>Remove a key from this scope only. Does not affect parent scopes.</summary>
    public bool Remove(string key) => _values.Remove(key);

    /// <summary>Clear all keys in this scope only. Does not affect parent scopes.</summary>
    public void Clear() => _values.Clear();

    /// <summary>Create a new child scope whose parent is this blackboard.</summary>
    public Blackboard CreateChildScope() => new(this);
}
