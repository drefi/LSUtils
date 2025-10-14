using System.Linq;

namespace LSUtils.ProcessSystem;
/// <summary>
/// Utility class providing helper methods for working with LSProcessNodeCondition delegates.
/// Centralizes condition evaluation logic and provides consistent behavior across the LSProcessing system.
/// </summary>
/// <remarks>
/// <para>
/// <b>Centralized Condition Management:</b><br/>
/// This static utility class provides standardized methods for evaluating node conditions, ensuring 
/// consistent behavior across all processing scenarios and simplifying condition handling throughout 
/// the LSProcessing system architecture.
/// </para>
/// <para>
/// <b>Design Benefits:</b><br/>
/// • Consistency: Uniform condition evaluation logic across all node types<br/>
/// • Error Handling: Centralized exception handling for condition failures<br/>
/// • Performance: Optimized evaluation with short-circuit logic<br/>
/// • Maintainability: Single point of truth for condition evaluation behavior
/// </para>
/// </remarks>
public static class LSProcessHelpers {
    public static bool SplitNode(string node, out string child, out string[]? grandChildren) {
        var parts = node.Split('.');
        // No grandchildren
        if (parts.Length <= 1) {
            child = node;
            grandChildren = null;
            return false;
        }
        child = parts[0];
        //join grandchildren
        grandChildren = parts.Length > 1 ? parts.Skip(1).ToArray() : null;
        return true;
    }
    /// <summary>
    /// Evaluates all conditions in a node's condition delegate chain.
    /// All conditions must return true for the node to be considered eligible for processing.
    /// </summary>
    /// <param name="process">The process being executed, providing context for condition evaluation.</param>
    /// <param name="node">The node whose conditions should be evaluated.</param>
    /// <returns>True if all conditions are met, false if any condition fails or throws an exception.</returns>
    /// <remarks>
    /// <para>
    /// <b>Evaluation Strategy:</b><br/>
    /// • Short-Circuit Evaluation: Returns false immediately when the first condition fails for optimal performance<br/>
    /// • Default Behavior: Returns true if the node has no conditions (null Conditions property)<br/>
    /// • All Must Pass: All conditions in the delegate chain must return true for overall success<br/>
    /// • Invocation Order: Conditions are evaluated in the order they were added to the delegate chain
    /// </para>
    /// <para>
    /// <b>Integration Points:</b><br/>
    /// This method is typically called by processing layer nodes before adding children to their available 
    /// children collection for execution. It serves as the primary gate-keeping mechanism for conditional 
    /// node execution within the processing pipeline.
    /// </para>
    /// <para>
    /// <b>Error Resilience:</b><br/>
    /// If any condition throws an exception during evaluation, it is treated as a failed condition (returns false). 
    /// This defensive approach prevents individual condition failures from propagating and crashing the entire 
    /// processing pipeline, ensuring system stability.
    /// </para>
    /// <para>
    /// <b>Performance Characteristics:</b><br/>
    /// The method uses GetInvocationList() to iterate through composed delegates, enabling efficient evaluation 
    /// while maintaining the ability to compose complex conditional logic from simpler building blocks.
    /// </para>
    /// </remarks>
    /// <example>
    /// Called internally by layer nodes during processing:
    /// <code>
    /// if (LSProcessConditions.IsMet(currentProcess, childNode)) {
    ///     availableChildren.Add(childNode);
    /// }
    /// </code>
    /// </example>
    public static bool IsMet(ILSProcess process, ILSProcessNode node) {
        if (node.Conditions == null) return true; // No conditions means always true

        foreach (LSProcessNodeCondition condition in node.Conditions.GetInvocationList()) {
            if (!condition(process, node)) return false;
        }
        return true;
    }

    /// <summary>
    /// Updates or combines node conditions based on the specified override behavior.
    /// Handles delegate composition and provides default condition fallback logic.
    /// </summary>
    /// <param name="overrideExisting">If true, replaces existing conditions; if false, combines with existing conditions.</param>
    /// <param name="existingConditions">The current condition delegate chain, or null if no conditions exist.</param>
    /// <param name="conditions">Array of new conditions to add or replace with. Null conditions are ignored.</param>
    /// <returns>The resulting condition delegate chain after update operations, or null if no valid conditions.</returns>
    /// <remarks>
    /// <para>
    /// <b>Condition Management Strategy:</b><br/>
    /// • Override Mode: Replaces existing conditions with new ones, starting with default true condition<br/>
    /// • Combine Mode: Adds new conditions to existing condition chain using delegate composition<br/>
    /// • Default Fallback: Uses a default condition that always returns true when overriding with empty conditions<br/>
    /// • Null Safety: Automatically filters out null conditions from the input array
    /// </para>
    /// <para>
    /// <b>Delegate Composition:</b><br/>
    /// Conditions are combined using the += operator, creating a multicast delegate where all conditions
    /// must return true for the overall evaluation to succeed (AND logic).
    /// </para>
    /// <para>
    /// <b>Usage Context:</b><br/>
    /// This method is used internally by the fluent builder pattern when configuring node conditions
    /// through methods like WithCondition() and similar builder operations.
    /// </para>
    /// </remarks>
    /// <example>
    /// Override existing conditions:
    /// <code>
    /// var newConditions = UpdateConditions(true, existingConditions, condition1, condition2);
    /// </code>
    /// 
    /// Combine with existing conditions:
    /// <code>
    /// var combinedConditions = UpdateConditions(false, existingConditions, newCondition);
    /// </code>
    /// </example>
    internal static LSProcessNodeCondition? UpdateConditions(bool overrideExisting, LSProcessNodeCondition? existingConditions, params LSProcessNodeCondition?[] conditions) {
        // Default condition always returns true - used as base when overriding
        var defaultCondition = (LSProcessNodeCondition)((process, node) => true);
        if (overrideExisting) {
            // when overriding, always start with default condition
            // if no conditions is provided, assume that the user want to use the default condition
            existingConditions = defaultCondition;
        }

        // no conditions provided
        if (conditions == null || conditions.Length == 0) {
            // If overriding, return default condition; else keep existing conditions
            return existingConditions;
        }
        // Conditions provided - handle override vs combine logic

        // Add all non-null conditions to the delegate chain
        foreach (var condition in conditions) {
            if (condition != null) {
                existingConditions += condition;
            }
        }


        return existingConditions;
    }
}
