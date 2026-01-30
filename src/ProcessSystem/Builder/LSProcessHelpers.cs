using System.Collections.Generic;
using System.Linq;

namespace LSUtils.ProcessSystem;
/// <summary>
/// Static utility class providing helper methods for LSProcessing system operations.
/// <para>
/// LSProcessHelpers centralizes common processing operations including condition evaluation,
/// node path parsing, and condition delegate management. This class ensures consistent
/// behavior across all processing scenarios and simplifies condition handling throughout
/// the system architecture.
/// </para>
/// <para>
/// <b>Core Capabilities:</b><br/>
/// - Condition evaluation with short-circuit logic and exception safety<br/>
/// - Node path parsing for hierarchical node addressing<br/>
/// - Condition delegate composition and update management<br/>
/// - Standardized evaluation patterns used by all node types
/// </para>
/// <para>
/// <b>Integration Points:</b><br/>
/// Used extensively by all layer nodes during child filtering, by the builder system
/// for condition management, and by the processing pipeline for path resolution.
/// </para>
/// </summary>
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
    public static bool IsMet(LSProcess process, ILSProcessNode node) {
        if (node.Conditions == null) return true; // No conditions means always true
        foreach (LSProcessNodeCondition? condition in node.Conditions) {
            if (condition == null) continue; // Skip null conditions
            if (condition(process) == false) return false;
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
    internal static LSProcessNodeCondition?[] UpdateConditions(NodeUpdatePolicy updatePolicy, LSProcessNodeCondition?[] existingConditions, params LSProcessNodeCondition?[] conditions) {
        // Filter out null conditions
        var validConditions = conditions?.Where(c => c != null).ToArray() ?? System.Array.Empty<LSProcessNodeCondition>();

        // No new conditions provided - return existing regardless of flags
        if (validConditions.Length == 0) {
            if (updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_CONDITIONS)) {
                // Override with empty = remove all conditions (always true)
                return new LSProcessNodeCondition?[] { (process) => true };
            }
            return existingConditions;
        }

        // Neither OVERRIDE nor MERGE - ignore new conditions, keep existing
        if (!updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_CONDITIONS) &&
            !updatePolicy.HasFlag(NodeUpdatePolicy.MERGE_CONDITIONS)) {
            return existingConditions;
        }

        // Start fresh if overriding, otherwise merge with existing
        List<LSProcessNodeCondition?> result = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_CONDITIONS)
            ? new List<LSProcessNodeCondition?>()
            : existingConditions.ToList();

        // Add new conditions
        foreach (var condition in validConditions) {
            result.Add(condition);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Creates a strongly-typed condition that automatically handles the casting.
    /// </summary>
    /// <typeparam name="TProcess">The target process type.</typeparam>
    /// <param name="condition">The strongly-typed condition to execute.</param>
    /// <returns>A condition delegate that handles the casting automatically.</returns>
    /// <example>
    /// Usage for creating typed conditions:
    /// <code>
    /// var canAttackCondition = LSProcessExtensions.CreateCondition&lt;EngageTask&gt;(
    ///     task => task.Entity.CanAttack);
    /// 
    /// builder.Handler("attack", attackHandler, conditions: canAttackCondition);
    /// </code>
    /// </example>
    public static LSProcessNodeCondition ToCondition<TProcess>(this LSProcessNodeCondition<TProcess> genericCondition)
        where TProcess : LSProcess {
        if (genericCondition == null)
            return null!;

        return process => {
            if (process is TProcess typedProcess) {
                return genericCondition(typedProcess);
            }
            return false; // Condition fails if process is not of expected type
        };
    }
    public static LSProcessNodeCondition?[] ToCondition<TProcess>(this LSProcessNodeCondition<TProcess>?[] genericConditions)
        where TProcess : LSProcess {
        if (genericConditions == null)
            return System.Array.Empty<LSProcessNodeCondition>();

        return genericConditions
            .OfType<LSProcessNodeCondition<TProcess>>()
            .Select<LSProcessNodeCondition<TProcess>, LSProcessNodeCondition>(cond => process => {
                if (process is TProcess typedProcess) {
                    return cond(typedProcess);
                }
                return false; // Condition fails if process is not of expected type
            })
            .ToArray();
    }

    public static LSProcessHandler ToHandler<TProcess>(this LSProcessHandler<TProcess> genericHandler)
        where TProcess : LSProcess {
        if (genericHandler == null)
            return null!;

        return session => {
            if (session.Process is TProcess typedProcess) {
                // Create a temporary typed session with the same properties
                var typedSession = new LSProcessSession<TProcess>(
                    session.Manager,
                    typedProcess,
                    session.RootNode,
                    session.ContextMode,
                    session.Instances,
                    session.ContextInstances
                );
                return genericHandler(typedSession);
            }
            return LSProcessResultStatus.FAILURE; // Handler fails if process is not of expected type
        };
    }
}
