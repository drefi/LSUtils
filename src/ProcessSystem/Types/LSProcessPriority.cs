using System;

namespace LSUtils.ProcessSystem;

/// <summary>
/// Ordered node priority. Higher values execute first; equal priorities are ordered
/// by the node's explicit order. Named values cover the supported public policy.
/// </summary>
public readonly record struct LSProcessPriority : IComparable<LSProcessPriority> {
    public static LSProcessPriority MINIMAL { get; } = new(-2);
    public static LSProcessPriority LOW { get; } = new(-1);
    public static LSProcessPriority NORMAL => default;
    public static LSProcessPriority HIGH { get; } = new(1);
    public static LSProcessPriority CRITICAL { get; } = new(2);

    private readonly sbyte _rank;
    public int Value => _rank + 2;
    public string Name => _rank switch {
        -2 => nameof(MINIMAL),
        -1 => nameof(LOW),
        0 => nameof(NORMAL),
        1 => nameof(HIGH),
        2 => nameof(CRITICAL),
        _ => throw new InvalidOperationException("Invalid process priority.")
    };

    private LSProcessPriority(sbyte rank) => _rank = rank;

    public int CompareTo(LSProcessPriority other) => _rank.CompareTo(other._rank);
    public override string ToString() => Name;
}
