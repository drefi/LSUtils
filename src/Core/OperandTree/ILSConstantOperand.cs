namespace LSUtils.OperandTree;
/// <summary>
/// Interface for constant operands that hold a single immutable numeric value.
/// </summary>
/// <typeparam name="TValue">The numeric type of the constant value.</typeparam>
public interface ILSConstantOperand<TValue> : ILSNumericOperand<TValue> where TValue : System.Numerics.INumber<TValue> {
    /// <summary>
    /// Gets the constant value held by this operand.
    /// </summary>
    TValue Value { get; }
}
