using System;

namespace LSUtils;
#region Interfaces
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
    T Resolve<T>(IOperandVisitor visitor);
}

public interface IOperandVisitor {
    float Visit(ILSConstantOperand<float> node);
    float Visit(ILSVarOperand<float> node);
    float Visit(ILSBinaryOperand<float> node);
    float Visit(ILSUnaryOperand<float> node);
    float Visit(ILSTernaryConditionalOperand<float> node);
    int Visit(ILSConstantOperand<int> node);
    int Visit(ILSVarOperand<int> node);
    int Visit(ILSBinaryOperand<int> node);
    int Visit(ILSUnaryOperand<int> node);
    int Visit(ILSTernaryConditionalOperand<int> node);
    bool Visit(ILSConditionalOperand node);
    bool Visit(ILSBinaryConditionalOperand node);
    bool Visit(ILSUnaryBooleanOperand node);
}
public interface IVariableProvider<out T> where T : System.Numerics.INumber<T> {
    // ID: "Source", Key: AttributeDefiner asset
    T GetValue(string id, object key);
}
public class LSEvaluator<T> : IOperandVisitor where T : System.Numerics.INumber<T> {
    private readonly IVariableProvider<T> _provider;
    public LSEvaluator(IVariableProvider<T> provider) {
        _provider = provider;
    }
    public float Visit(ILSConstantOperand<float> node) => node.Value;
    public float Visit(ILSVarOperand<float> node) => (float)((object)_provider.GetValue(node.ID, node.Key));
    public float Visit(ILSBinaryOperand<float> node) => ILSNumericOperand<float>.BinaryOperation(node.Operator, node.Left.Resolve(this), node.Right.Resolve(this));
    public float Visit(ILSUnaryOperand<float> node) => ILSNumericOperand<float>.UnaryOperation(node.Operator, node.Operand.Resolve(this));
    public float Visit(ILSTernaryConditionalOperand<float> node) => node.Condition.Resolve(this) ? node.TrueOperand.Resolve(this) : node.FalseOperand.Resolve(this);
    public int Visit(ILSConstantOperand<int> node) => node.Value;
    public int Visit(ILSVarOperand<int> node) => (int)((object)_provider.GetValue(node.ID, node.Key));
    public int Visit(ILSBinaryOperand<int> node) => ILSNumericOperand<int>.BinaryOperation(node.Operator, node.Left.Resolve(this), node.Right.Resolve(this));
    public int Visit(ILSUnaryOperand<int> node) => ILSNumericOperand<int>.UnaryOperation(node.Operator, node.Operand.Resolve(this));
    public int Visit(ILSTernaryConditionalOperand<int> node) => node.Condition.Resolve(this) ? node.TrueOperand.Resolve(this) : node.FalseOperand.Resolve(this);
    public bool Visit(ILSConditionalOperand node) {
        var leftValue = (IComparable)node.Left.Resolve<object>(this);
        var rightValue = (IComparable)node.Right.Resolve<object>(this);
        return ILSBooleanOperand.ComparisonOperation(node.Operator, leftValue.CompareTo(rightValue));
    }
    public bool Visit(ILSBinaryConditionalOperand node) => ILSBooleanOperand.BooleanOperation(node.Operator, node.Left, node.Right, this);
    public bool Visit(ILSUnaryBooleanOperand node) => !node.Operand.Resolve(this);
}
/// <summary>
/// Generic operand interface that produces a strongly-typed result.
/// </summary>
/// <typeparam name="T">The type of value this operand evaluates to.</typeparam>
public interface ILSOperand<out T> : ILSOperand {
    T Resolve(IOperandVisitor visitor);
}

/// <summary>
/// Operand interface for numeric types that support mathematical operations.
/// </summary>
/// <typeparam name="T">The numeric type, must implement INumber&lt;T&gt;.</typeparam>
public interface ILSNumericOperand<out T> : ILSOperand<T> where T : System.Numerics.INumber<T>, System.IComparable<T> {
    public static T UnaryOperation(UnaryOperator @operator, T value) {
        return @operator switch {
            UnaryOperator.Negate => -value,
            UnaryOperator.Abs => T.Abs(value),
            UnaryOperator.Floor => T.CreateChecked(LSMath.Floor(double.CreateChecked(value))),
            UnaryOperator.Ceil => T.CreateChecked(LSMath.Ceil(double.CreateChecked(value))),
            UnaryOperator.Round => T.CreateChecked(LSMath.Round(double.CreateChecked(value))),
            UnaryOperator.Sqrt => T.CreateChecked(LSMath.Sqrt(double.CreateChecked(value))),
            UnaryOperator.Square => value * value,
            _ => throw new LSNotImplementedException($"Unary operator {@operator} not implemented.")
        };
    }
    public static T BinaryOperation(MathOperator operation, T left, T right) {
        return operation switch {
            MathOperator.None => right,
            MathOperator.Add => left + right,
            MathOperator.Subtract => left - right,
            MathOperator.Multiply => left * right,
            MathOperator.Divide => left / right,
            MathOperator.Power => T.CreateChecked(System.Math.Pow(double.CreateChecked(left), double.CreateChecked(right))),
            MathOperator.Min => T.Min(left, right),
            MathOperator.Max => T.Max(left, right),
            MathOperator.Modulo => left % right,
            _ => throw new LSNotImplementedException($"Operator {operation} not implemented in {nameof(ILSNumericOperand<T>)}."),
        };
    }
}
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
/// <summary>
/// Interface for variable operands that retrieve their value from a provider based on an ID and key.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ILSVarOperand<T> : ILSNumericOperand<T> where T : System.Numerics.INumber<T> {
    /// <summary>
    /// Identifier for the variable source (e.g., "EntityA.Health"). Used by the provider to determine where to get the value from.
    /// </summary>
    string ID { get; }
    /// <summary>
    /// Key object that provides additional context for the provider to retrieve the correct value. Depends on the provider's implementation.
    /// </summary>
    object Key { get; }
}
/// <summary>
/// Operand interface specifically for boolean operations and conditions.
/// </summary>
public interface ILSBooleanOperand : ILSOperand<bool> {
    public static bool BooleanOperation(BooleanOperator @operator, ILSBooleanOperand l, ILSBooleanOperand right, IOperandVisitor visitor) {
        var leftValue = l.Resolve<bool>(visitor);

        return @operator switch {
            BooleanOperator.And => leftValue && right.Resolve<bool>(visitor),
            BooleanOperator.Or => leftValue || right.Resolve<bool>(visitor),
            BooleanOperator.Xor => leftValue ^ right.Resolve<bool>(visitor),
            BooleanOperator.Nand => !(leftValue && right.Resolve<bool>(visitor)),
            BooleanOperator.Nor => !(leftValue || right.Resolve<bool>(visitor)),
            _ => throw new LSNotImplementedException($"Boolean operator {@operator} not implemented.")
        };
    }
    public static bool ComparisonOperation(ComparisonOperator @operator, int comparison) {
        return @operator switch {
            ComparisonOperator.Equal => comparison == 0,
            ComparisonOperator.NotEqual => comparison != 0,
            ComparisonOperator.GreaterThan => comparison > 0,
            ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
            ComparisonOperator.LessThan => comparison < 0,
            ComparisonOperator.LessThanOrEqual => comparison <= 0,
            _ => throw new LSNotImplementedException($"Comparison operator {@operator} not implemented.")
        };

    }
}

/// <summary>
/// Interface for binary mathematical operations that combine two numeric operands.
/// </summary>
/// <typeparam name="T">The numeric type of the operands and result.</typeparam>
public interface ILSBinaryOperand<T> : ILSNumericOperand<T> where T : System.Numerics.INumber<T> {
    /// <summary>
    /// Gets the left operand of the binary operation.
    /// </summary>
    public ILSNumericOperand<T> Left { get; }

    /// <summary>
    /// Gets the right operand of the binary operation.
    /// </summary>
    public ILSNumericOperand<T> Right { get; }

    /// <summary>
    /// Gets the mathematical operator to apply between the operands.
    /// </summary>
    public MathOperator Operator { get; }
}

/// <summary>
/// Interface for conditional comparison operations that compare two values and return a boolean result.
/// </summary>
public interface ILSConditionalOperand : ILSBooleanOperand {
    /// <summary>
    /// Gets the comparison operator used to compare the operands.
    /// </summary>
    ComparisonOperator Operator { get; }

    /// <summary>
    /// Gets the left operand to compare.
    /// </summary>
    ILSOperand Left { get; }

    /// <summary>
    /// Gets the right operand to compare.
    /// </summary>
    ILSOperand Right { get; }
}

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

/// <summary>
/// Interface for unary boolean operations that negate a boolean operand.
/// </summary>
public interface ILSUnaryBooleanOperand : ILSBooleanOperand {
    /// <summary>
    /// Gets the boolean operand to negate.
    /// </summary>
    ILSBooleanOperand Operand { get; }
}

#endregion

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
    public T Resolve(IOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSConstantOperand<float>)(object)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSConstantOperand<int>)(object)this);
        }
        throw new LSNotImplementedException($"Constant operand of type {typeof(T)} is not supported in the visitor.");
    }
    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(IOperandVisitor visitor) {
        return (TOperand)(object)Resolve(visitor);
    }

    /// <summary>
    /// Implicit conversion from a numeric value to a ConstantOperand.
    /// </summary>
    public static implicit operator LSConstantOperand<T>(T value) => new(value);
}
public class LSVarOperand<T> : ILSVarOperand<T> where T : System.Numerics.INumber<T> {
    public string ID { get; }
    public object Key { get; }

    public LSVarOperand(string id, object key) {
        ID = id;
        Key = key;
    }

    public T Resolve(IOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSVarOperand<float>)(object)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSVarOperand<int>)(object)this);
        }
        throw new LSNotImplementedException($"Variable operand of type {typeof(T)} is not supported in the visitor.");
    }

    TOperand ILSOperand.Resolve<TOperand>(IOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);
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
    public T Resolve(IOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSBinaryOperand<float>)(object)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSBinaryOperand<int>)(object)this);
        }
        throw new LSNotImplementedException($"Binary operand of type {typeof(T)} is not supported in the visitor.");
    }
    TOperand ILSOperand.Resolve<TOperand>(IOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

}

/// <summary>
/// Compares two comparable values and returns a boolean result.
/// Supports all standard comparison operators (==, !=, &lt;, &gt;, &lt;=, &gt;=).
/// </summary>
public class LSConditionalOperand : ILSConditionalOperand {
    public ComparisonOperator Operator { get; }
    public ILSOperand Left { get; }
    public ILSOperand Right { get; }

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
    public bool Resolve(IOperandVisitor visitor) => visitor.Visit(this);

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(IOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

}

/// <summary>
/// Performs binary boolean operations on two boolean operands.
/// Implements short-circuit evaluation for AND and OR operators to improve performance.
/// </summary>
public class LSBinaryConditionalOperand : ILSBinaryConditionalOperand {
    public BooleanOperator Operator { get; }
    public ILSBooleanOperand Left { get; }
    public ILSBooleanOperand Right { get; }

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
    public bool Resolve(IOperandVisitor visitor) => visitor.Visit(this);

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(IOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);
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
    public T Resolve(IOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSUnaryOperand<float>)(object)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSUnaryOperand<int>)(object)this);
        }
        throw new LSNotImplementedException($"Unary operand of type {typeof(T)} is not supported");
    }

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(IOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);
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
    public T Resolve(IOperandVisitor visitor) {
        if (typeof(T) == typeof(float)) {
            return (T)(object)visitor.Visit((ILSTernaryConditionalOperand<float>)(object)this);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)visitor.Visit((ILSTernaryConditionalOperand<int>)(object)this);
        }
        throw new LSNotImplementedException($"Ternary conditional operand of type {typeof(T)} is not supported");
    }

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(IOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);
}

/// <summary>
/// Negates a boolean operand, inverting its true/false value.
/// Implements the logical NOT operation.
/// </summary>
public class LSUnaryBooleanOperand : ILSUnaryBooleanOperand {
    public ILSBooleanOperand Operand { get; }

    /// <summary>
    /// Creates a unary boolean negation operation.
    /// </summary>
    /// <param name="operand">The boolean operand to negate.</param>
    public LSUnaryBooleanOperand(ILSBooleanOperand operand) {
        Operand = operand;
    }

    /// <inheritdoc/>
    public bool Resolve(IOperandVisitor visitor) => visitor.Visit(this);

    /// <inheritdoc/>
    TOperand ILSOperand.Resolve<TOperand>(IOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);
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
