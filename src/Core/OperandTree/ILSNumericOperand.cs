namespace LSUtils.OperandTree;


public interface ILSNumericOperand : ILSOperand {
    bool Evaluate<TValue>(ILSOperandVisitor visitor, out TValue? result, params object?[] args) where TValue : System.Numerics.INumber<TValue>;
}
/// <summary>
/// Operand interface for numeric types that support mathematical operations.
/// </summary>
/// <typeparam name="TValue">The numeric type, must implement INumber&lt;T&gt;.</typeparam>
public interface ILSNumericOperand<TValue> : ILSNumericOperand where TValue : System.Numerics.INumber<TValue> {
    /// <summary>
    /// Gets the numeric value of this operand, if it can be evaluated.
    /// </summary>
    /// <param name="visitor">The visitor to use for evaluating any sub-operands.</param>
    /// <param name="value">The resulting value if evaluation is successful; otherwise, default.</param>
    /// <param name="args">Additional arguments that may be needed for evaluation.</param>
    /// <returns>True if the operand was successfully evaluated to a value; otherwise, false.</returns>
    bool Evaluate(ILSOperandVisitor visitor, out TValue? value, params object?[] args);
}

public static class NumericOperandExtensions {
    public static TValue UnaryOperation<TValue>(this TValue value, UnaryOperator @operator) where TValue : System.Numerics.INumber<TValue> {
        return @operator switch {
            UnaryOperator.Negate => -value,
            UnaryOperator.Abs => TValue.Abs(value),
            UnaryOperator.Floor => TValue.CreateChecked(LSMath.Floor(double.CreateChecked(value))),
            UnaryOperator.Ceil => TValue.CreateChecked(LSMath.Ceil(double.CreateChecked(value))),
            UnaryOperator.Round => TValue.CreateChecked(LSMath.Round(double.CreateChecked(value))),
            UnaryOperator.Sqrt => TValue.CreateChecked(LSMath.Sqrt(double.CreateChecked(value))),
            UnaryOperator.Square => value * value,
            _ => throw new LSNotImplementedException($"Unary operator {@operator} not implemented in {nameof(NumericOperandExtensions)}.{nameof(UnaryOperation)}.")
        };
    }
    public static TValue BinaryOperation<TValue>(this TValue left, MathOperator @operator, TValue right) where TValue : System.Numerics.INumber<TValue> {
        return @operator switch {
            MathOperator.None => right,
            MathOperator.Add => left + right,
            MathOperator.Subtract => left - right,
            MathOperator.Multiply => left * right,
            MathOperator.Divide => left / right,
            MathOperator.Power => TValue.CreateChecked(System.Math.Pow(double.CreateChecked(left), double.CreateChecked(right))),
            MathOperator.Min => TValue.Min(left, right),
            MathOperator.Max => TValue.Max(left, right),
            MathOperator.Modulo => left % right,
            _ => throw new LSNotImplementedException($"Operator {@operator} not implemented in {nameof(NumericOperandExtensions)}.{nameof(BinaryOperation)}."),
        };
    }
}
