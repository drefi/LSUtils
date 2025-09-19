namespace LSUtils.EventSystem;

/// <summary>
/// Delegate that represents a condition function for determining whether a node should be processed.
/// Provides pluggable logic for conditional execution based on event data and node context.
/// </summary>
/// <param name="event">The event being processed, containing data for condition evaluation.</param>
/// <param name="node">The node being evaluated, providing access to node metadata for condition logic.</param>
/// <returns>True if the condition is met and the node should be processed, false otherwise.</returns>
/// <remarks>
/// <para><strong>Composition:</strong></para>
/// <para>Multiple conditions can be combined using delegate composition (+=). All conditions</para>
/// <para>in the delegate chain must return true for the node to be eligible for processing.</para>
/// 
/// <para><strong>Performance Considerations:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Efficiency</strong>: Conditions are evaluated before processing, so they should be fast</description></item>
/// <item><description><strong>Side Effects</strong>: Conditions should be pure functions without side effects</description></item>
/// <item><description><strong>Determinism</strong>: Conditions should return consistent results for the same inputs</description></item>
/// </list>
/// 
/// <para><strong>Common Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Event Type Filtering</strong>: Only process specific event types</description></item>
/// <item><description><strong>State Validation</strong>: Check preconditions before processing</description></item>
/// <item><description><strong>Permission Checks</strong>: Verify authorization before execution</description></item>
/// <item><description><strong>Resource Availability</strong>: Ensure required resources are available</description></item>
/// </list>
/// 
/// <para><strong>Default Behavior:</strong></para>
/// <para>If no conditions are specified, nodes use a default condition that always returns true,</para>
/// <para>making them eligible for processing in all scenarios.</para>
/// </remarks>
public delegate bool LSEventCondition(ILSEvent @event, ILSEventNode node);

/// <summary>
/// Utility class providing helper methods for working with LSEventCondition delegates.
/// Centralizes condition evaluation logic and provides consistent behavior across the system.
/// </summary>
public static class LSEventConditions { 
    /// <summary>
    /// Evaluates all conditions in a node's condition delegate chain.
    /// All conditions must return true for the node to be considered eligible for processing.
    /// </summary>
    /// <param name="event">The event being processed.</param>
    /// <param name="node">The node whose conditions should be evaluated.</param>
    /// <returns>True if all conditions are met, false if any condition fails.</returns>
    /// <remarks>
    /// <para><strong>Evaluation Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Short-Circuit</strong>: Returns false immediately when the first condition fails</description></item>
    /// <item><description><strong>Empty Chain</strong>: Returns true if the node has no conditions (default condition)</description></item>
    /// <item><description><strong>All Required</strong>: All conditions in the delegate chain must pass</description></item>
    /// </list>
    /// 
    /// <para><strong>Usage Pattern:</strong></para>
    /// <para>This method is typically called by layer nodes before adding children to their</para>
    /// <para>_availableChildren list for processing.</para>
    /// 
    /// <para><strong>Exception Handling:</strong></para>
    /// <para>If a condition throws an exception, it is treated as a failed condition (returns false).</para>
    /// <para>This prevents individual condition failures from crashing the entire processing pipeline.</para>
    /// </remarks>
    public static bool IsMet(ILSEvent @event, ILSEventNode node) {
        foreach (LSEventCondition c in node.Conditions.GetInvocationList()) {
            if (!c(@event, node)) return false;
        }
        return true;
    }
}
