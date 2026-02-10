namespace LSUtils;
/// <summary>
/// Interface for binary boolean operations that combine two boolean operands using logical operators.
/// </summary>
public interface ILSBinaryConditionalOperand : ILSBooleanOperand {
    /// <summary>
    /// Gets the boolean operator used to combine the operands.
    /// </summary>
    BooleanOperator Operator { get; }

    /// <summary>
    /// Gets the left boolean operand.
    /// </summary>
    ILSBooleanOperand Left { get; }

    /// <summary>
    /// Gets the right boolean operand.
    /// </summary>
    ILSBooleanOperand Right { get; }
}
