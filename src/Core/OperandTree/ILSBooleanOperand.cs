namespace LSUtils;
/// <summary>
/// Operand interface specifically for boolean operations and conditions.
/// </summary>
public interface ILSBooleanOperand : ILSOperand<bool?> {
    bool? Value { get; }
    public static bool BooleanOperation(BooleanOperator @operator, ILSBooleanOperand left, ILSBooleanOperand right, ILSOperandVisitor visitor) {
        if (left.Accept(visitor, out var leftValue) == false || leftValue == null) {
            throw new LSInvalidOperationException("Failed to resolve left operand for boolean operation.");
        }
        var resolveRight = (ILSBooleanOperand operand) => {
            if (operand.Accept(visitor, out var rightValue) == false || rightValue == null) {
                throw new LSInvalidOperationException("Failed to resolve right operand for boolean operation.");
            }
            return rightValue.Value;
        };

        return @operator switch {
            BooleanOperator.And => leftValue.Value && resolveRight(right),
            BooleanOperator.Or => leftValue.Value || resolveRight(right),
            BooleanOperator.Xor => leftValue.Value ^ resolveRight(right),
            BooleanOperator.Nand => !(leftValue.Value && resolveRight(right)),
            BooleanOperator.Nor => !(leftValue.Value || resolveRight(right)),
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
