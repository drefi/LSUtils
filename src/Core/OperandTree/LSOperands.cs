namespace LSUtils.OperandTree;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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


    public virtual bool Evaluate(ILSOperandVisitor visitor, out T? value, params object?[] args) {
        return visitor.Visit(this, out value, args);
    }

    public virtual bool Accept(ILSVisitor visitor, params object?[] args) {
        return visitor.Visit(this, args);
    }

    public bool Evaluate<TValue>(ILSOperandVisitor visitor, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        if (Evaluate(visitor, out var value, args) == false || value == null) {
            result = default;
            return false;
        }
        if (value is TValue castValue) {
            result = castValue;
            return true;
        }
        result = default;
        return false;
    }


    /// <summary>
    /// Implicit conversion from a numeric value to a ConstantOperand.
    /// </summary>
    public static implicit operator LSConstantOperand<T>(T value) => new(value);
    public static implicit operator T(LSConstantOperand<T> operand) => operand.Value;
}

public class LSBooleanConstantOperand : ILSBooleanOperand {
    public bool? Value { get; }

    public LSBooleanConstantOperand(bool value) {
        Value = value;
    }

    public virtual bool Evaluate(ILSOperandVisitor visitor, out bool? value, params object?[] args) {
        return visitor.Visit(this, out value, args);
    }
    public virtual bool Accept(ILSVisitor visitor, params object?[] args) {
        return visitor.Visit(this, args);
    }

    /// <summary>
    /// Implicit conversion from a boolean value to a BooleanConstantOperand.
    /// </summary>
    public static implicit operator LSBooleanConstantOperand(bool value) => new(value);
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
    public virtual bool Evaluate(ILSOperandVisitor visitor, out T? value, params object?[] parameters) {
        return visitor.Visit(this, out value, parameters);
    }
    public bool Evaluate<TValue>(ILSOperandVisitor visitor, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        if (Evaluate(visitor, out var value, args) == false || value == null) {
            result = default;
            return false;
        }
        if (value is TValue castValue) {
            result = castValue;
            return true;
        }
        result = default;
        return false;
    }

    public virtual bool Accept(ILSVisitor visitor, params object?[] args) {
        return visitor.Visit(this, args);
    }
    public static implicit operator LSBinaryOperand<T>((ILSNumericOperand<T> left, ILSNumericOperand<T> right, MathOperator op) tuple) => new(tuple.left, tuple.right, tuple.op);
}

/// <summary>
/// Compares two comparable values and returns a boolean result.
/// Supports all standard comparison operators (==, !=, &lt;, &gt;, &lt;=, &gt;=).
/// </summary>
public class LSConditionalOperand : ILSComparerOperand {
    public ComparisonOperator Operator { get; }
    public ILSNumericOperand Left { get; }
    public ILSNumericOperand Right { get; }
    public bool? Value { get; protected set; }

    /// <summary>
    /// Creates a comparison operation between two operands.
    /// </summary>
    /// <param name="operator">The comparison operator to use.</param>
    /// <param name="left">Left operand to compare.</param>
    /// <param name="right">Right operand to compare.</param>
    public LSConditionalOperand(ComparisonOperator @operator, ILSNumericOperand left, ILSNumericOperand right) {
        Operator = @operator;
        Left = left;
        Right = right;
        Value = null;
    }
    public virtual bool Evaluate(ILSOperandVisitor visitor, out bool? value, params object?[] args) {
        return visitor.Visit(this, out value, args);
    }

    public virtual bool Accept(ILSVisitor visitor, params object?[] args) {
        return visitor.Visit(this, args);
    }

}

/// <summary>
/// Performs binary boolean operations on two boolean operands.
/// Implements short-circuit evaluation for AND and OR operators to improve performance.
/// </summary>
public class LSBinaryConditionalOperand : ILSConditionalOperand {
    public BooleanOperator Operator { get; }
    public ILSBooleanOperand Left { get; }
    public ILSBooleanOperand Right { get; }
    public bool? Value => null; // Value is determined by evaluating the operands, not stored directly.
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

    public virtual bool Evaluate(ILSOperandVisitor visitor, out bool? value, params object?[] parameters) {
        return visitor.Visit(this, out value, parameters);
    }

    public virtual bool Accept(ILSVisitor visitor, params object?[] args) {
        List<object?> resultList = args.OfType<List<object?>>().FirstOrDefault() ?? new List<object?>();
        return visitor.Visit(this, resultList, args);
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

    public bool Evaluate(ILSOperandVisitor visitor, out T? value, params object?[] parameters) {
        return visitor.Visit(this, out value, parameters);
    }
    public bool Evaluate<TValue>(ILSOperandVisitor visitor, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        if (Evaluate(visitor, out var value, args) == false || value == null) {
            result = default;
            return false;
        }
        if (value is TValue castValue) {
            result = castValue;
            return true;
        }
        result = default;
        return false;
    }

    public bool Accept(ILSVisitor visitor, params object?[] args) {
        List<object?> resultList = args.OfType<List<object?>>().FirstOrDefault() ?? new List<object?>();
        return visitor.Visit(this, resultList, args);
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

    public bool Evaluate(ILSOperandVisitor visitor, out T? value, params object?[] parameters) {
        return visitor.Visit(this, out value, parameters);
    }
    public bool Evaluate<TValue>(ILSOperandVisitor visitor, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        if (Evaluate(visitor, out var value, args) == false || value == null) {
            result = default;
            return false;
        }
        if (value is TValue castValue) {
            result = castValue;
            return true;
        }
        result = default;
        return false;
    }

    public bool Accept(ILSVisitor visitor, params object?[] args) {
        List<object?> resultList = args.OfType<List<object?>>().FirstOrDefault() ?? new List<object?>();
        return visitor.Visit(this, resultList, args);
    }
}

/// <summary>
/// Negates a boolean operand, inverting its true/false value.
/// Implements the logical NOT operation.
/// </summary>
public class LSNegateBooleanOperand : ILSNegateBooleanOperand {
    public ILSBooleanOperand Operand { get; }
    public bool? Value { get; protected set; }

    /// <summary>
    /// Creates a unary boolean negation operation.
    /// </summary>
    /// <param name="operand">The boolean operand to negate.</param>
    public LSNegateBooleanOperand(ILSBooleanOperand operand) {
        Operand = operand;
        Value = null;
    }

    public bool Evaluate(ILSOperandVisitor visitor, out bool? value, params object?[] parameters) {
        return visitor.Visit(this, out value, parameters);
    }
    public bool Accept(ILSVisitor visitor, params object?[] args) {
        List<object?> resultList = args.OfType<List<object?>>().FirstOrDefault() ?? new List<object?>();
        return visitor.Visit(this, resultList, args);
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
public static class MathOperatorExtensions {
    public static MathOperator Negate(this MathOperator op) {
        return op switch {
            MathOperator.Add => MathOperator.Subtract,
            MathOperator.Subtract => MathOperator.Add,
            MathOperator.Multiply => MathOperator.Divide,
            MathOperator.Divide => MathOperator.Multiply,
            _ => op
        };
    }
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
