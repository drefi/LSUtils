using System.Collections.Generic;

namespace LSUtils;
#region Operand Implementations

/// <summary>
/// Represents a constant value that doesn't change during evaluation.
/// Most efficient operand type as it requires no computation.
/// </summary>
/// <typeparam name="T">The numeric type of the constant.</typeparam>
public class LSConstantOperand<T> : ILSConstantOperand<T> where T : System.Numerics.INumber<T> {
    public T Value { get; }

    /// <summary>
    /// Creates a new constant operand with the specified value.
    /// </summary>
    /// <param name="value">The constant value to return during evaluation.</param>
    public LSConstantOperand(T value) {
        Value = value;
    }
    object? ILSOperand.Resolve(ILSOperandVisitor visitor) {
        return (T?)Resolve(visitor);
    }

    public TOperand? Resolve<TOperand>(ILSOperandVisitor visitor) {
        return (TOperand?)(object?)Resolve(visitor);
    }

    public T? Resolve(ILSOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSConstantOperand<float>)(object)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSConstantOperand<int>)(object)this);
        }
        throw new LSNotImplementedException($"Constant operand of type {typeof(T)} is not supported in the visitor.");

    }



    /// <summary>
    /// Implicit conversion from a numeric value to a ConstantOperand.
    /// </summary>
    public static implicit operator LSConstantOperand<T>(T value) => new(value);
}
public class LSBooleanConstantOperand : ILSBooleanOperand {
    public bool Value { get; }

    public LSBooleanConstantOperand(bool value) {
        Value = value;
    }
    public bool Resolve(ILSOperandVisitor visitor) => visitor.Visit(this);
    TOperand ILSOperand.Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);
    object? ILSOperand.Resolve(ILSOperandVisitor visitor) => Resolve(visitor);


    /// <summary>
    /// Implicit conversion from a boolean value to a BooleanConstantOperand.
    /// </summary>
    public static implicit operator LSBooleanConstantOperand(bool value) => new(value);
}
public class LSVarOperand : ILSVarOperand {
    public IReadOnlyList<object?> Parameters { get; }
    public LSVarOperand(params object?[] parameters) {
        Parameters = parameters ?? new object[0];
    }

    public object? Resolve(ILSOperandVisitor visitor) {
        return visitor.Visit(this);
    }

    public TOperand? Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand?)Resolve(visitor)!;
}
public class LSNumericVarOperand<T> : LSVarOperand, ILSNumericOperand<T> where T : System.Numerics.INumber<T> {
    public LSNumericVarOperand(params object?[] parameters) : base(parameters) { }

    T? ILSOperand<T>.Resolve(ILSOperandVisitor visitor) {
        var result = Resolve(visitor);
        if (result is T typedResult) {
            return typedResult;
        }
        throw new LSInvalidOperationException($"Variable operand resolved to a value of type {result?.GetType().Name}, but expected type was {typeof(T).Name}.");
    }

}

/// <summary>
/// Performs binary mathematical operations on two numeric operands.
/// Evaluates both operands before applying the operation.
/// </summary>
/// <typeparam name="T">The numeric type of the operands and result.</typeparam>
public class LSBinaryOperand<T> : ILSBinaryOperand<T> where T : System.Numerics.INumber<T> {
    public ILSNumericOperand<T> Left { get; }
    public ILSNumericOperand<T> Right { get; }
    public MathOperator Operator { get; }

    /// <summary>
    /// Creates a binary mathematical operation.
    /// </summary>
    /// <param name="left">Left operand to evaluate.</param>
    /// <param name="right">Right operand to evaluate.</param>
    /// <param name="op">Mathematical operator to apply.</param>
    public LSBinaryOperand(ILSNumericOperand<T> left, ILSNumericOperand<T> right, MathOperator op) {
        Left = left;
        Right = right;
        Operator = op;
    }
    public T Resolve(ILSOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSBinaryOperand<float>)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSBinaryOperand<int>)this);
        }
        throw new LSNotImplementedException($"Binary operand of type {typeof(T)} is not supported in the visitor.");

    }
    TOperand ILSOperand.Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

    object? ILSOperand.Resolve(ILSOperandVisitor visitor) {
        return Resolve(visitor);
    }
}

/// <summary>
/// Compares two comparable values and returns a boolean result.
/// Supports all standard comparison operators (==, !=, &lt;, &gt;, &lt;=, &gt;=).
/// </summary>
public class LSConditionalOperand : ILSConditionalOperand {
    public ComparisonOperator Operator { get; }
    public ILSOperand Left { get; }
    public ILSOperand Right { get; }
    public bool Value => throw new LSNotImplementedException("Value property is not implemented for LSConditionalOperand. Use Resolve method with a visitor instead.");

    /// <summary>
    /// Creates a comparison operation between two operands.
    /// </summary>
    /// <param name="operator">The comparison operator to use.</param>
    /// <param name="left">Left operand to compare.</param>
    /// <param name="right">Right operand to compare.</param>
    public LSConditionalOperand(ComparisonOperator @operator, ILSOperand left, ILSOperand right) {
        Operator = @operator;
        Left = left;
        Right = right;
    }

    /// <inheritdoc/>
    public bool Resolve(ILSOperandVisitor visitor) => visitor.Visit(this);

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

    object? ILSOperand.Resolve(ILSOperandVisitor visitor) {
        return Resolve(visitor);
    }
}

/// <summary>
/// Performs binary boolean operations on two boolean operands.
/// Implements short-circuit evaluation for AND and OR operators to improve performance.
/// </summary>
public class LSBinaryConditionalOperand : ILSBinaryConditionalOperand {
    public BooleanOperator Operator { get; }
    public ILSBooleanOperand Left { get; }
    public ILSBooleanOperand Right { get; }
    public bool Value => throw new LSNotImplementedException("Value property is not implemented for LSBinaryConditionalOperand. Use Resolve method with a visitor instead.");
    /// <summary>
    /// Creates a binary boolean operation.
    /// </summary>
    /// <param name="operator">The boolean operator to apply.</param>
    /// <param name="left">Left boolean operand.</param>
    /// <param name="right">Right boolean operand (may not be evaluated due to short-circuiting).</param>
    public LSBinaryConditionalOperand(BooleanOperator @operator, ILSBooleanOperand left, ILSBooleanOperand right) {
        Operator = @operator;
        Left = left;
        Right = right;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses short-circuit evaluation for AND and OR:
    /// - AND: if left is false, right is not evaluated
    /// - OR: if left is true, right is not evaluated
    /// </remarks>
    public bool Resolve(ILSOperandVisitor visitor) => visitor.Visit(this);

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

    object? ILSOperand.Resolve(ILSOperandVisitor visitor) {
        return Resolve(visitor);
    }

}

/// <summary>
/// Performs unary mathematical operations on a single numeric operand.
/// Examples: negation, absolute value, rounding, square root.
/// </summary>
/// <typeparam name="T">The numeric type of the operand and result.</typeparam>
public class LSUnaryOperand<T> : ILSUnaryOperand<T> where T : System.Numerics.INumber<T> {
    public ILSNumericOperand<T> Operand { get; }
    public UnaryOperator Operator { get; }

    /// <summary>
    /// Creates a unary mathematical operation.
    /// </summary>
    /// <param name="operand">The operand to apply the operation to.</param>
    /// <param name="op">The unary operator to apply.</param>
    public LSUnaryOperand(ILSNumericOperand<T> operand, UnaryOperator op) {
        Operand = operand;
        Operator = op;
    }

    /// <inheritdoc/>
    public T Resolve(ILSOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSUnaryOperand<float>)(object)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSUnaryOperand<int>)(object)this);
        }
        throw new LSNotImplementedException($"Unary operand of type {typeof(T)} is not supported");
    }

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

    /// <inheritdoc/>
    object? ILSOperand.Resolve(ILSOperandVisitor visitor) {
        return Resolve(visitor);
    }
}

/// <summary>
/// Implements ternary conditional logic (condition ? trueValue : falseValue).
/// Only evaluates the branch that matches the condition result, improving efficiency.
/// </summary>
/// <typeparam name="T">The numeric type of the result.</typeparam>
public class LSTernaryConditionalOperand<T> : ILSTernaryConditionalOperand<T> where T : System.Numerics.INumber<T> {
    public ILSBooleanOperand Condition { get; }
    public ILSNumericOperand<T> TrueOperand { get; }
    public ILSNumericOperand<T> FalseOperand { get; }

    /// <summary>
    /// Creates a ternary conditional operation.
    /// </summary>
    /// <param name="condition">Boolean condition to evaluate.</param>
    /// <param name="trueOperand">Operand to evaluate if condition is true.</param>
    /// <param name="falseOperand">Operand to evaluate if condition is false.</param>
    public LSTernaryConditionalOperand(ILSBooleanOperand condition, ILSNumericOperand<T> trueOperand, ILSNumericOperand<T> falseOperand) {
        Condition = condition;
        TrueOperand = trueOperand;
        FalseOperand = falseOperand;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Only the selected branch is evaluated, avoiding unnecessary computation.
    /// </remarks>
    public T Resolve(ILSOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSTernaryConditionalOperand<float>)(object)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSTernaryConditionalOperand<int>)(object)this);
        }
        throw new LSNotImplementedException($"Ternary conditional operand of type {typeof(T)} is not supported");
    }

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

    /// <inheritdoc/>
    object? ILSOperand.Resolve(ILSOperandVisitor visitor) {
        return Resolve(visitor);
    }
}

/// <summary>
/// Negates a boolean operand, inverting its true/false value.
/// Implements the logical NOT operation.
/// </summary>
public class LSUnaryBooleanOperand : ILSNegateBooleanOperand {
    public ILSBooleanOperand Operand { get; }
    public bool Value => throw new LSNotImplementedException("Value property is not implemented for LSUnaryBooleanOperand. Use Resolve method with a visitor instead.");

    /// <summary>
    /// Creates a unary boolean negation operation.
    /// </summary>
    /// <param name="operand">The boolean operand to negate.</param>
    public LSUnaryBooleanOperand(ILSBooleanOperand operand) {
        Operand = operand;
    }

    /// <inheritdoc/>
    public bool Resolve(ILSOperandVisitor visitor) => visitor.Visit(this);

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

    /// <inheritdoc/>
    object? ILSOperand.Resolve(ILSOperandVisitor visitor) {
        return Resolve(visitor);
    }
}

#endregion

#region Enums

/// <summary>
/// Comparison operators for conditional evaluations.
/// </summary>
public enum ComparisonOperator {
    /// <summary>Equals (==)</summary>
    Equal,
    /// <summary>Not equals (!=)</summary>
    NotEqual,
    /// <summary>Greater than (&gt;)</summary>
    GreaterThan,
    /// <summary>Greater than or equal (&gt;=)</summary>
    GreaterThanOrEqual,
    /// <summary>Less than (&lt;)</summary>
    LessThan,
    /// <summary>Less than or equal (&lt;=)</summary>
    LessThanOrEqual,
}

/// <summary>
/// Boolean operators for logical operations.
/// </summary>
public enum BooleanOperator {
    /// <summary>Logical AND (both must be true)</summary>
    And,
    /// <summary>Logical OR (at least one must be true)</summary>
    Or,
    /// <summary>Logical XOR (exactly one must be true)</summary>
    Xor,
    /// <summary>Logical NAND (NOT AND)</summary>
    Nand,
    /// <summary>Logical NOR (NOT OR)</summary>
    Nor,
}

/// <summary>
/// Binary mathematical operators.
/// </summary>
public enum MathOperator {
    /// <summary>No operation, returns right operand (useful for assignment/override)</summary>
    None,
    /// <summary>Addition (+)</summary>
    Add,
    /// <summary>Subtraction (-)</summary>
    Subtract,
    /// <summary>Multiplication (*)</summary>
    Multiply,
    /// <summary>Division (/)</summary>
    Divide,
    /// <summary>Exponentiation (^)</summary>
    Power,
    /// <summary>Minimum value of two operands</summary>
    Min,
    /// <summary>Maximum value of two operands</summary>
    Max,
    /// <summary>Modulo operation (%)</summary>
    Modulo,
}

/// <summary>
/// Unary mathematical operators that operate on a single value.
/// </summary>
public enum UnaryOperator {
    /// <summary>Negation (change sign)</summary>
    Negate,
    /// <summary>Absolute value</summary>
    Abs,
    /// <summary>Round down to nearest integer</summary>
    Floor,
    /// <summary>Round up to nearest integer</summary>
    Ceil,
    /// <summary>Round to nearest integer</summary>
    Round,
    /// <summary>Square root</summary>
    Sqrt,
    /// <summary>Square (value * value)</summary>
    Square
}

#endregion
