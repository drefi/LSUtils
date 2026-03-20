namespace LSUtils.OperandTree;
/// <summary>
/// Interface for unary mathematical operations that transform a single numeric operand.
/// </summary>
/// <typeparam name="T">The numeric type of the operand and result.</typeparam>
public interface ILSUnaryOperand<T> : ILSNumericOperand<T> where T : System.Numerics.INumber<T> {
    /// <summary>
    /// Gets the operand to transform.
    /// </summary>
    public ILSNumericOperand<T> Operand { get; }

    /// <summary>
    /// Gets the unary operator to apply to the operand.
    /// </summary>
    public UnaryOperator Operator { get; }
}
