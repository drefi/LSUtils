using System;

namespace LSUtils.ProcessSystem;

/// <summary>
/// Immutable result of a process node. The outcome controls execution flow while
/// an optional typed payload explains or carries the result without string keys.
/// </summary>
public readonly struct LSProcessResult : IEquatable<LSProcessResult> {
    private enum ResultKind : byte { NotExecuted, Success, Failure, Waiting, Cancelled, Undetermined }

    private readonly ResultKind _kind;
    private readonly object? _payload;
    private readonly Type? _payloadType;

    private LSProcessResult(ResultKind kind, object? payload = null, Type? payloadType = null) {
        _kind = kind;
        _payload = payload;
        _payloadType = payloadType;
    }

    public static LSProcessResult NotExecuted => default;
    public static LSProcessResult Success => new(ResultKind.Success);
    public static LSProcessResult Failure => new(ResultKind.Failure);
    public static LSProcessResult Waiting => new(ResultKind.Waiting);
    public static LSProcessResult Cancelled => new(ResultKind.Cancelled);
    public static LSProcessResult Undetermined => new(ResultKind.Undetermined);

    public static LSProcessResult Succeeded<T>(T payload) => WithPayload(ResultKind.Success, payload);
    public static LSProcessResult Failed<T>(T payload) => WithPayload(ResultKind.Failure, payload);
    public static LSProcessResult WaitingFor<T>(T payload) => WithPayload(ResultKind.Waiting, payload);
    public static LSProcessResult CancelledBy<T>(T payload) => WithPayload(ResultKind.Cancelled, payload);
    public static LSProcessResult UndeterminedBy<T>(T payload) => WithPayload(ResultKind.Undetermined, payload);

    public bool IsNotExecuted => _kind == ResultKind.NotExecuted;
    public bool IsSuccess => _kind == ResultKind.Success;
    public bool IsFailure => _kind == ResultKind.Failure;
    public bool IsWaiting => _kind == ResultKind.Waiting;
    public bool IsCancelled => _kind == ResultKind.Cancelled;
    public bool IsUndetermined => _kind == ResultKind.Undetermined;
    public bool IsTerminal => IsSuccess || IsFailure || IsCancelled;
    public bool HasPayload => _payloadType != null;
    public Type? PayloadType => _payloadType;

    public bool TryGetPayload<T>(out T? payload) {
        if (_payloadType != null && typeof(T).IsAssignableFrom(_payloadType)) {
            payload = (T?)_payload;
            return true;
        }
        payload = default;
        return false;
    }

    public T? GetPayload<T>() => TryGetPayload<T>(out var payload)
        ? payload : throw new InvalidOperationException(
            $"Result '{this}' does not carry a payload assignable to {typeof(T).FullName}.");

    /// <summary>Flow equality intentionally ignores diagnostic payload values.</summary>
    public bool Equals(LSProcessResult other) => _kind == other._kind;
    public override bool Equals(object? obj) => obj is LSProcessResult other && Equals(other);
    public override int GetHashCode() => (int)_kind;
    public static bool operator ==(LSProcessResult left, LSProcessResult right) => left.Equals(right);
    public static bool operator !=(LSProcessResult left, LSProcessResult right) => !left.Equals(right);

    public override string ToString() => _payloadType == null
        ? _kind.ToString() : $"{_kind}<{_payloadType.Name}>";

    private static LSProcessResult WithPayload<T>(ResultKind kind, T payload) =>
        new(kind, payload, typeof(T));
}

/// <summary>Provenance retained when an inverter reverses a terminal outcome.</summary>
public sealed record LSProcessInversion(LSProcessResult Original);
