namespace LSUtils;
/// <summary>
/// Interface for binary mathematical operations that combine two numeric operands.
/// </summary>
/// <typeparam name="T">The numeric type of the operands and result.</typeparam>
public interface ILSBinaryOperand<T> : ILSNumericOperand<T> where T : System.Numerics.INumber<T> {
    /// <summary>
    /// Gets the left operand of the binary operation.
    /// </summary>
    public ILSNumericOperand<T> Left { get; }

    /// <summary>
    /// Gets the right operand of the binary operation.
    /// </summary>
    public ILSNumericOperand<T> Right { get; }

    /// <summary>
    /// Gets the mathematical operator to apply between the operands.
    /// </summary>
    public MathOperator Operator { get; }
}
