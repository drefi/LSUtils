namespace LSUtils;
/// <summary>
/// Base interface for operands that can be evaluated to produce a value.
/// Forms the foundation of a composable formula system.
/// </summary>
public interface ILSOperand {
    /// <summary>
    /// Evaluates the operand and returns the result.
    /// </summary>
    /// <param name="context">Evaluation context containing necessary information and state.</param>
    /// <returns>The evaluated value, or null if evaluation fails.</returns>
    T Resolve<T>(ILSOperandVisitor visitor);
}
/// <summary>
/// Generic operand interface that produces a strongly-typed result.
/// </summary>
/// <typeparam name="T">The type of value this operand evaluates to.</typeparam>
public interface ILSOperand<out T> : ILSOperand {
    T Resolve(ILSOperandVisitor visitor);
}
