namespace LSUtils.OperandTree;
/// <summary>
/// Interface for conditional comparison operations that compare two values and return a boolean result.
/// </summary>
public interface ILSComparerOperand : ILSBooleanOperand {
    /// <summary>
    /// Gets the comparison operator used to compare the operands.
    /// </summary>
    ComparisonOperator Operator { get; }

    /// <summary>
    /// Gets the left operand to compare.
    /// </summary>
    ILSNumericOperand Left { get; }

    /// <summary>
    /// Gets the right operand to compare.
    /// </summary>
    ILSNumericOperand Right { get; }
}
