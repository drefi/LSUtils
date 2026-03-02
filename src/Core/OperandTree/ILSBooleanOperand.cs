namespace LSUtils;
/// <summary>
/// Operand interface specifically for boolean operations and conditions.
/// </summary>
public interface ILSBooleanOperand : ILSOperand<bool?> {
    bool? Value { get; }
    public static bool BooleanOperation(BooleanOperator @operator, bool left, bool right) {
        return @operator switch {
            BooleanOperator.And => left && right,
            BooleanOperator.Or => left || right,
            BooleanOperator.Xor => left ^ right,
            BooleanOperator.Nand => !(left && right),
            BooleanOperator.Nor => !(left || right),
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
