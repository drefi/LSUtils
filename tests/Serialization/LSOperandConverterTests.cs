namespace LSUtils.Tests.Serialization;

using System.Text.Json;
using LSUtils.OperandTree;
using NUnit.Framework;

[TestFixture]
public class LSOperandConverterTests {
    private JsonSerializerOptions _options = null!;

    [SetUp]
    public void SetUp() {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new LSOperandConverter());
    }

    [Test]
    public void LSOperandConverter_ShouldSerializeAndDeserialize() {
        var operand = new LSBinaryOperand<float>(
            new LSConstantOperand<float>(5),
            new LSConstantOperand<float>(10),
            MathOperator.Multiply
        );

        var json = JsonSerializer.Serialize<ILSOperand>(operand, _options);
        var deserializedOperand = JsonSerializer.Deserialize<ILSOperand>(json, _options);

        Assert.That(deserializedOperand, Is.Not.Null);
        Assert.That(deserializedOperand, Is.InstanceOf<LSBinaryOperand<float>>());

        var binaryOperand = (LSBinaryOperand<float>)deserializedOperand!;
        Assert.That(binaryOperand.Operator, Is.EqualTo(MathOperator.Multiply));
        Assert.That(binaryOperand.Left, Is.InstanceOf<LSConstantOperand<float>>());
        Assert.That(binaryOperand.Right, Is.InstanceOf<LSConstantOperand<float>>());

        var leftConstant = (LSConstantOperand<float>)binaryOperand.Left;
        var rightConstant = (LSConstantOperand<float>)binaryOperand.Right;

        Assert.That(leftConstant.Value, Is.EqualTo(5));
        Assert.That(rightConstant.Value, Is.EqualTo(10));
    }

    [Test]
    public void LSOperandConverter_ShouldSerializeAndDeserializeUnaryFloat() {
        var operand = new LSUnaryOperand<float>(
            new LSConstantOperand<float>(-2.5f),
            UnaryOperator.Abs
        );

        var json = JsonSerializer.Serialize<ILSOperand>(operand, _options);
        var deserializedOperand = JsonSerializer.Deserialize<ILSOperand>(json, _options);

        Assert.That(deserializedOperand, Is.Not.Null);
        Assert.That(deserializedOperand, Is.InstanceOf<LSUnaryOperand<float>>());

        var unaryOperand = (LSUnaryOperand<float>)deserializedOperand!;
        Assert.That(unaryOperand.Operator, Is.EqualTo(UnaryOperator.Abs));
        Assert.That(unaryOperand.Operand, Is.InstanceOf<LSConstantOperand<float>>());

        var constant = (LSConstantOperand<float>)unaryOperand.Operand;
        Assert.That(constant.Value, Is.EqualTo(-2.5f));
    }

    [Test]
    public void LSOperandConverter_ShouldSerializeAndDeserializeBooleanConstant() {
        var operand = new LSBooleanConstantOperand(true);

        var json = JsonSerializer.Serialize<ILSOperand>(operand, _options);
        var deserializedOperand = JsonSerializer.Deserialize<ILSOperand>(json, _options);

        Assert.That(deserializedOperand, Is.Not.Null);
        Assert.That(deserializedOperand, Is.InstanceOf<LSBooleanConstantOperand>());

        var constant = (LSBooleanConstantOperand)deserializedOperand!;
        Assert.That(constant.Value, Is.True);
    }

    [Test]
    public void LSOperandConverter_ShouldSerializeAndDeserializeBooleanNegation() {
        var operand = new LSNegateBooleanOperand(new LSBooleanConstantOperand(true));

        var json = JsonSerializer.Serialize<ILSOperand>(operand, _options);
        var deserializedOperand = JsonSerializer.Deserialize<ILSOperand>(json, _options);

        Assert.That(deserializedOperand, Is.Not.Null);
        Assert.That(deserializedOperand, Is.InstanceOf<LSNegateBooleanOperand>());

        var negateOperand = (LSNegateBooleanOperand)deserializedOperand!;
        Assert.That(negateOperand.Operand, Is.InstanceOf<LSBooleanConstantOperand>());

        var nested = (LSBooleanConstantOperand)negateOperand.Operand;
        Assert.That(nested.Value, Is.True);
    }

    [Test]
    public void LSOperandConverter_ShouldThrowWhenTypeDiscriminatorMissing() {
        const string json = "{\"value\":5}";

        var act = () => JsonSerializer.Deserialize<ILSOperand>(json, _options);

        Assert.That(act, Throws.TypeOf<JsonException>()
            .With.Message.Contains("missing $type"));
    }

    [Test]
    public void LSOperandConverter_ShouldThrowWhenTypeDiscriminatorIsUnsupported() {
        const string json = "{\"$type\":\"unknown\"}";

        var act = () => JsonSerializer.Deserialize<ILSOperand>(json, _options);

        Assert.That(act, Throws.TypeOf<JsonException>()
            .With.Message.Contains("Unsupported operand type: unknown"));
    }
}
