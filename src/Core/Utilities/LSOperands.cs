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
    object Evaluate(IEvaluationContext context);
}

/// <summary>
/// Generic operand interface that produces a strongly-typed result.
/// </summary>
/// <typeparam name="T">The type of value this operand evaluates to.</typeparam>
public interface ILSOperand<T> : ILSOperand {
    /// <summary>
    /// Evaluates the operand and returns the strongly-typed result.
    /// </summary>
    /// <param name="context">Evaluation context containing necessary information and state.</param>
    /// <returns>The evaluated value of type T, or null if evaluation fails.</returns>
    new T Evaluate(IEvaluationContext context);
}

/// <summary>
/// Operand interface for numeric types that support mathematical operations.
/// </summary>
/// <typeparam name="T">The numeric type, must implement INumber&lt;T&gt;.</typeparam>
public interface ILSNumericOperand<T> : ILSOperand<T> where T : System.Numerics.INumber<T>, System.IComparable<T> {
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
/// Operand interface specifically for boolean operations and conditions.
/// </summary>
public interface ILSBooleanOperand : ILSOperand<bool> {
    public static bool BooleanOperation(BooleanOperator @operator, ILSBooleanOperand l, ILSBooleanOperand right, IEvaluationContext context) {
        var leftValue = l.Evaluate(context);

        return @operator switch {
            BooleanOperator.And => leftValue && right.Evaluate(context),
            BooleanOperator.Or => leftValue || right.Evaluate(context),
            BooleanOperator.Xor => leftValue ^ right.Evaluate(context),
            BooleanOperator.Nand => !(leftValue && right.Evaluate(context)),
            BooleanOperator.Nor => !(leftValue || right.Evaluate(context)),
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

    /// <inheritdoc/>
    public T Evaluate(IEvaluationContext context) => Value;

    /// <inheritdoc/>
    object ILSOperand.Evaluate(IEvaluationContext context) => Value;

    /// <summary>
    /// Implicit conversion from a numeric value to a ConstantOperand.
    /// </summary>
    public static implicit operator LSConstantOperand<T>(T value) => new(value);
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

    /// <inheritdoc/>
    public T Evaluate(IEvaluationContext context) {
        var l = Left.Evaluate(context)!;
        var r = Right.Evaluate(context)!;

        return ILSNumericOperand<T>.BinaryOperation(Operator, l, r);
    }

    /// <inheritdoc/>
    object ILSOperand.Evaluate(IEvaluationContext context) => Evaluate(context);

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
    public bool Evaluate(IEvaluationContext context) {
        var leftValue = Left.Evaluate(context)!;
        var rightValue = Right.Evaluate(context)!;
        if (leftValue is System.IComparable leftComp && rightValue is System.IComparable rightComp) {
            var comparison = leftComp.CompareTo(rightComp);
            return ILSBooleanOperand.ComparisonOperation(Operator, comparison);
        }
        throw new LSInvalidOperationException("Operands are not comparable.");
    }

    /// <inheritdoc/>
    object ILSOperand.Evaluate(IEvaluationContext context) => Evaluate(context);

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
    public bool Evaluate(IEvaluationContext context) {
        return ILSBooleanOperand.BooleanOperation(Operator, Left, Right, context);
    }

    /// <inheritdoc/>
    object ILSOperand.Evaluate(IEvaluationContext context) => Evaluate(context);
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
    public T Evaluate(IEvaluationContext context) {
        var value = Operand.Evaluate(context)!;

        return ILSNumericOperand<T>.UnaryOperation(Operator, value);
    }

    /// <inheritdoc/>
    object ILSOperand.Evaluate(IEvaluationContext context) => Evaluate(context);
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
    public T Evaluate(IEvaluationContext context) {
        return Condition.Evaluate(context)
            ? TrueOperand.Evaluate(context)!
            : FalseOperand.Evaluate(context)!;
    }

    /// <inheritdoc/>
    object ILSOperand.Evaluate(IEvaluationContext context) => Evaluate(context);
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
    public bool Evaluate(IEvaluationContext context) => !Operand.Evaluate(context);
    
    /// <inheritdoc/>
    object ILSOperand.Evaluate(IEvaluationContext context) => Evaluate(context);
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