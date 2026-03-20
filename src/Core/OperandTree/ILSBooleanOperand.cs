namespace LSUtils.OperandTree;
/// <summary>
/// Operand interface specifically for boolean operations and conditions.
/// </summary>
public interface ILSBooleanOperand : ILSOperand<bool?> {
    bool? Value { get; }
}

public static class BooleanOperandExtensions {
    public static bool BinaryOperation(this bool left, bool right, BooleanOperator @operator) {
        return @operator switch {
            BooleanOperator.And => left && right,
            BooleanOperator.Or => left || right,
            BooleanOperator.Xor => left ^ right,
            BooleanOperator.Nand => !(left && right),
            BooleanOperator.Nor => !(left || right),
            _ => throw new LSNotImplementedException($"Boolean operator {@operator} not implemented.")
        };
    }
    public static bool? Compare(this System.IComparable? left, System.IComparable? right, ComparisonOperator @operator) {
        if (left == null || right == null) {
            return null;
        }
        int comparison = left.CompareTo(right);
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
