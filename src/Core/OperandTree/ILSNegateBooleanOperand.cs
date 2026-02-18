namespace LSUtils;

/// <summary>
/// Interface for unary boolean operations that negate a boolean operand.
/// </summary>
public interface ILSNegateBooleanOperand : ILSBooleanOperand {
    /// <summary>
    /// Gets the boolean operand to negate.
    /// </summary>
    ILSBooleanOperand Operand { get; }
}
