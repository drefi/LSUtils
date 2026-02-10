namespace LSUtils;
/// <summary>
/// Interface for constant operands that hold a single immutable numeric value.
/// </summary>
/// <typeparam name="T">The numeric type of the constant value.</typeparam>
public interface ILSConstantOperand<T> : ILSNumericOperand<T> where T : System.Numerics.INumber<T> {
    /// <summary>
    /// Gets the constant value held by this operand.
    /// </summary>
    T Value { get; }
}
