namespace LSUtils;
/// <summary>
/// Interface for ternary conditional operations that select between two numeric operands based on a boolean condition.
/// Implements the pattern: condition ? trueValue : falseValue
/// </summary>
/// <typeparam name="T">The numeric type of the result operands.</typeparam>
public interface ILSTernaryConditionalOperand<T> : ILSNumericOperand<T> where T : System.Numerics.INumber<T> {
    /// <summary>
    /// Gets the boolean condition that determines which operand to evaluate.
    /// </summary>
    ILSBooleanOperand Condition { get; }

    /// <summary>
    /// Gets the operand to evaluate when the condition is true.
    /// </summary>
    ILSNumericOperand<T> TrueOperand { get; }

    /// <summary>
    /// Gets the operand to evaluate when the condition is false.
    /// </summary>
    ILSNumericOperand<T> FalseOperand { get; }
}
