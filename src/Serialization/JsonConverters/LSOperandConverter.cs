namespace LSUtils.OperandTree;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class LSOperandConverter : JsonConverter<ILSOperand> {
    public override ILSOperand? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("$type", out var typeElement)) {
            throw new JsonException("Operand payload missing $type.");
        }

        var kind = typeElement.GetString();
        return kind switch {
            "const:f32" => new LSConstantOperand<float>(root.GetProperty("value").GetSingle()),

            "bool:const" => new LSBooleanConstantOperand(root.GetProperty("value").GetBoolean()),

            "unary:f32" => ReadUnaryFloat(root, options),

            "binary:f32" => ReadBinaryFloat(root, options),

            "bool:not" => ReadBoolNot(root, options),

            _ => throw new JsonException($"Unsupported operand type: {kind}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ILSOperand value, JsonSerializerOptions options) {
        writer.WriteStartObject();

        switch (value) {
            case ILSConstantOperand<float> constantF32:
                writer.WriteString("$type", "const:f32");
                writer.WriteNumber("value", constantF32.Value);
                break;

            case ILSNegateBooleanOperand notBool:
                writer.WriteString("$type", "bool:not");
                writer.WritePropertyName("operand");
                JsonSerializer.Serialize(writer, (ILSOperand)notBool.Operand, options);
                break;

            case ILSBooleanOperand boolConstant:
                writer.WriteString("$type", "bool:const");
                writer.WriteBoolean("value", boolConstant.Value ?? false);
                break;

            case ILSUnaryOperand<float> unaryF32:
                writer.WriteString("$type", "unary:f32");
                writer.WriteString("op", unaryF32.Operator.ToString());
                writer.WritePropertyName("operand");
                JsonSerializer.Serialize(writer, (ILSOperand)unaryF32.Operand, options);
                break;

            case ILSBinaryOperand<float> binaryF32:
                writer.WriteString("$type", "binary:f32");
                writer.WriteString("op", binaryF32.Operator.ToString());
                writer.WritePropertyName("left");
                JsonSerializer.Serialize(writer, (ILSOperand)binaryF32.Left, options);
                writer.WritePropertyName("right");
                JsonSerializer.Serialize(writer, (ILSOperand)binaryF32.Right, options);
                break;

            default:
                throw new JsonException($"Unsupported runtime operand type: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }

    private static LSUnaryOperand<float> ReadUnaryFloat(JsonElement root, JsonSerializerOptions options) {
        var opText = root.GetProperty("op").GetString() ?? throw new JsonException("Missing unary op.");
        var op = Enum.Parse<UnaryOperator>(opText, ignoreCase: true);

        var operand = JsonSerializer.Deserialize<ILSOperand>(root.GetProperty("operand").GetRawText(), options)
        as ILSNumericOperand<float>;

        if (operand == null) throw new JsonException("Unary operand must be numeric float.");

        return new LSUnaryOperand<float>(operand, op);
    }

    private static LSBinaryOperand<float> ReadBinaryFloat(JsonElement root, JsonSerializerOptions options) {
        var opText = root.GetProperty("op").GetString() ?? throw new JsonException("Missing binary op.");
        var op = Enum.Parse<MathOperator>(opText, ignoreCase: true);

        var left = JsonSerializer.Deserialize<ILSOperand>(root.GetProperty("left").GetRawText(), options)
        as ILSNumericOperand<float>;
        var right = JsonSerializer.Deserialize<ILSOperand>(root.GetProperty("right").GetRawText(), options)
        as ILSNumericOperand<float>;

        if (left == null || right == null) throw new JsonException("Binary operands must be numeric float.");

        return new LSBinaryOperand<float>(left, right, op);
    }

    private static LSNegateBooleanOperand ReadBoolNot(JsonElement root, JsonSerializerOptions options) {
        var operand = JsonSerializer.Deserialize<ILSOperand>(root.GetProperty("operand").GetRawText(), options)
        as ILSBooleanOperand;

        if (operand == null) throw new JsonException("Negate operand must be boolean.");

        return new LSNegateBooleanOperand(operand);
    }
}
