using System;
using System.Collections.Generic;

namespace LSUtils.ProcessSystem;

public enum LSProcessOutcome : byte {
    NotExecuted,
    Success,
    Failure,
    Waiting,
    Cancelled,
    Undetermined,
}

public sealed record LSProcessPayloadMemento(string TypeId, int Version, string Data);

public sealed record LSProcessResultMemento(
    LSProcessOutcome Outcome,
    LSProcessPayloadMemento? Payload = null);

public sealed record LSProcessExecutionNodeMemento(
    string NodeId,
    LSProcessDefinitionNodeKind Kind,
    bool Started,
    int Cursor,
    bool HasUndetermined,
    int ExecutionCount,
    IReadOnlyList<int> EligibleChildren,
    LSProcessResultMemento Status,
    LSProcessResultMemento LastResult,
    IReadOnlyList<LSProcessExecutionNodeMemento> Children);

/// <summary>Versioned state of one execution; callbacks remain in its matching definition.</summary>
public sealed record LSProcessExecutionMemento(
    int FormatVersion,
    Guid ProcessId,
    DateTime ProcessCreatedAtUtc,
    IReadOnlyDictionary<string, LSProcessPayloadMemento> ProcessData,
    IReadOnlyList<Guid> InstanceIds,
    IReadOnlyList<Guid> ContextInstanceIds,
    string DefinitionFingerprint,
    LSProcessExecutionNodeMemento Root) {
    public const int CurrentFormatVersion = 1;
}

public sealed class LSProcessPayloadCodecRegistry {
    private readonly Dictionary<Type, Encoder> _encoders = new();
    private readonly Dictionary<(string TypeId, int Version), Decoder> _decoders = new();

    public LSProcessPayloadCodecRegistry Register<T>(
        string typeId,
        int version,
        Func<T, string> encode,
        Func<string, T> decode) {
        if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("A payload type ID is required.", nameof(typeId));
        ArgumentNullException.ThrowIfNull(encode);
        ArgumentNullException.ThrowIfNull(decode);
        var key = (typeId, version);
        if (_encoders.ContainsKey(typeof(T)) || _decoders.ContainsKey(key)) {
            throw new ArgumentException($"A process payload codec is already registered for {typeof(T).FullName} or {typeId} v{version}.");
        }
        _encoders.Add(typeof(T), new Encoder(typeId, version, value => encode((T)value!)));
        _decoders.Add(key, new Decoder(typeof(T), data => decode(data)));
        return this;
    }

    internal LSProcessPayloadMemento Encode(object? payload, Type payloadType) {
        if (!_encoders.TryGetValue(payloadType, out var encoder)) {
            throw new InvalidOperationException($"No process payload codec is registered for {payloadType.FullName}.");
        }
        return new LSProcessPayloadMemento(encoder.TypeId, encoder.Version, encoder.Encode(payload));
    }

    internal (object? Value, Type Type) Decode(LSProcessPayloadMemento payload) {
        if (!_decoders.TryGetValue((payload.TypeId, payload.Version), out var decoder)) {
            throw new InvalidOperationException($"No process payload codec is registered for {payload.TypeId} v{payload.Version}.");
        }
        return (decoder.Decode(payload.Data), decoder.Type);
    }

    private sealed record Encoder(string TypeId, int Version, Func<object?, string> Encode);
    private sealed record Decoder(Type Type, Func<string, object?> Decode);
}
