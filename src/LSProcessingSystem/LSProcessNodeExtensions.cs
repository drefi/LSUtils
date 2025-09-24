namespace LSUtils.Processing;

/// <summary>
/// Provides extension methods and utility functions for working with LSProcessNode instances.
/// Contains helper methods for condition management and node configuration within the LSProcessing system.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purpose:</b><br/>
/// This static utility class centralizes common operations performed on processing nodes, particularly
/// around condition management and delegate composition. It provides internal helper methods used by
/// the LSProcessing system's fluent builder pattern and node configuration operations.
/// </para>
/// <para>
/// <b>Design Benefits:</b><br/>
/// • Centralized Logic: Common node operations are consolidated in one location<br/>
/// • Consistent Behavior: Ensures uniform handling of conditions across all node types<br/>
/// • Reusability: Shared functionality reduces code duplication throughout the system<br/>
/// • Maintainability: Single point of change for node extension behavior
/// </para>
/// </remarks>
public static class LSProcessNodeExtensions {
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
        // Default condition that always returns true - used as base when overriding
        var defaultCondition = (LSProcessNodeCondition)((process, node) => true);
        
        if (conditions == null || conditions.Length == 0) {
            // No conditions provided - set default condition only if overriding
            if (overrideExisting) {
                existingConditions = defaultCondition;
            }
        } else {
            // Conditions provided - handle override vs combine logic
            if (overrideExisting) {
                // Override mode: start fresh with default condition
                existingConditions = defaultCondition;
            }
            
            // Add all non-null conditions to the delegate chain
            foreach (var condition in conditions) {
                if (condition != null) {
                    existingConditions += condition;
                }
            }
        }
        
        return existingConditions;
    }
}
