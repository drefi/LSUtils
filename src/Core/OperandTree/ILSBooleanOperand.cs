namespace LSUtils;
/// <summary>
/// Operand interface specifically for boolean operations and conditions.
/// </summary>
public interface ILSBooleanOperand : ILSOperand<bool> {
    public static bool BooleanOperation(BooleanOperator @operator, ILSBooleanOperand l, ILSBooleanOperand right, ILSOperandVisitor visitor) {
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
