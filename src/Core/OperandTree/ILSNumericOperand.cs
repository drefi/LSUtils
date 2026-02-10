namespace LSUtils;
/// <summary>
/// Operand interface for numeric types that support mathematical operations.
/// </summary>
/// <typeparam name="T">The numeric type, must implement INumber&lt;T&gt;.</typeparam>
public interface ILSNumericOperand<out T> : ILSOperand<T> where T : System.Numerics.INumber<T>, System.IComparable<T> {
    public static T UnaryOperation(UnaryOperator @operator, T value) {
        return @operator switch {
            UnaryOperator.Negate => -value,
            UnaryOperator.Abs => T.Abs(value),
            UnaryOperator.Floor => T.CreateChecked(LSMath.Floor(double.CreateChecked(value))),
            UnaryOperator.Ceil => T.CreateChecked(LSMath.Ceil(double.CreateChecked(value))),
            UnaryOperator.Round => T.CreateChecked(LSMath.Round(double.CreateChecked(value))),
            UnaryOperator.Sqrt => T.CreateChecked(LSMath.Sqrt(double.CreateChecked(value))),
            UnaryOperator.Square => value * value,
            _ => throw new LSNotImplementedException($"Unary operator {@operator} not implemented.")
        };
    }
    public static T BinaryOperation(MathOperator operation, T left, T right) {
        return operation switch {
            MathOperator.None => right,
            MathOperator.Add => left + right,
            MathOperator.Subtract => left - right,
            MathOperator.Multiply => left * right,
            MathOperator.Divide => left / right,
            MathOperator.Power => T.CreateChecked(System.Math.Pow(double.CreateChecked(left), double.CreateChecked(right))),
            MathOperator.Min => T.Min(left, right),
            MathOperator.Max => T.Max(left, right),
            MathOperator.Modulo => left % right,
            _ => throw new LSNotImplementedException($"Operator {operation} not implemented in {nameof(ILSNumericOperand<T>)}."),
        };
    }
}
