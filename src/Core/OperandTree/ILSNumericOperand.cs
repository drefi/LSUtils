namespace LSUtils;
/// <summary>
/// Operand interface for numeric types that support mathematical operations.
/// </summary>
/// <typeparam name="TValue">The numeric type, must implement INumber&lt;T&gt;.</typeparam>
public interface ILSNumericOperand<TValue> : ILSOperand<TValue> where TValue : System.Numerics.INumber<TValue>, System.IComparable<TValue> {
    public static TValue UnaryOperation(UnaryOperator @operator, TValue value) {
        return @operator switch {
            UnaryOperator.Negate => -value,
            UnaryOperator.Abs => TValue.Abs(value),
            UnaryOperator.Floor => TValue.CreateChecked(LSMath.Floor(double.CreateChecked(value))),
            UnaryOperator.Ceil => TValue.CreateChecked(LSMath.Ceil(double.CreateChecked(value))),
            UnaryOperator.Round => TValue.CreateChecked(LSMath.Round(double.CreateChecked(value))),
            UnaryOperator.Sqrt => TValue.CreateChecked(LSMath.Sqrt(double.CreateChecked(value))),
            UnaryOperator.Square => value * value,
            _ => throw new LSNotImplementedException($"Unary operator {@operator} not implemented.")
        };
    }
    public static TValue BinaryOperation(MathOperator operation, TValue left, TValue right) {
        return operation switch {
            MathOperator.None => right,
            MathOperator.Add => left + right,
            MathOperator.Subtract => left - right,
            MathOperator.Multiply => left * right,
            MathOperator.Divide => left / right,
            MathOperator.Power => TValue.CreateChecked(System.Math.Pow(double.CreateChecked(left), double.CreateChecked(right))),
            MathOperator.Min => TValue.Min(left, right),
            MathOperator.Max => TValue.Max(left, right),
            MathOperator.Modulo => left % right,
            _ => throw new LSNotImplementedException($"Operator {operation} not implemented in {nameof(ILSNumericOperand<TValue>)}."),
        };
    }
}
