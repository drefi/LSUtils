namespace LSUtils.Processing;

/// <summary>
/// Delegate that represents a condition function for determining whether a node should be processed.
/// Provides pluggable logic for conditional execution based on process data and node context within the LSProcessing system.
/// </summary>
/// <param name="process">The process being executed, containing data for condition evaluation.</param>
/// <param name="node">The node being evaluated, providing access to node metadata for condition logic.</param>
/// <returns>True if the condition is met and the node should be processed, false otherwise.</returns>
/// <remarks>
/// <para>
/// <b>Conditional Processing Framework:</b><br/>
/// This delegate serves as the foundation for implementing conditional logic within the LSProcessing system,
/// enabling fine-grained control over when nodes are eligible for execution based on runtime conditions.
/// </para>
/// <para>
/// <b>Delegate Composition:</b><br/>
/// Multiple conditions can be combined using delegate composition (+=). All conditions in the delegate 
/// chain must return true for the node to be eligible for processing. This provides a powerful mechanism 
/// for creating complex conditional logic through composition of simpler conditions.
/// </para>
/// <para>
/// <b>Performance Considerations:</b><br/>
/// • Execution Efficiency: Conditions are evaluated before processing, so they should be fast and lightweight<br/>
/// • Pure Functions: Conditions should be pure functions without side effects to ensure predictable behavior<br/>
/// • Deterministic Results: Conditions should return consistent results for the same inputs across multiple evaluations<br/>
/// • Short-Circuit Evaluation: Evaluation stops at the first failing condition for optimal performance
/// </para>
/// <para>
/// <b>Common Usage Patterns:</b><br/>
/// • Process Type Filtering: Only execute nodes for specific process types or hierarchies<br/>
/// • State Validation: Check preconditions and process state before allowing execution<br/>
/// • Authorization Checks: Verify permissions and security constraints before processing<br/>
/// • Resource Availability: Ensure required resources, services, or dependencies are available<br/>
/// • Business Rules: Implement domain-specific business logic for conditional execution
/// </para>
/// <para>
/// <b>Default Behavior:</b><br/>
/// If no conditions are specified (null Conditions property), nodes use implicit default behavior 
/// that makes them eligible for processing in all scenarios, equivalent to a condition that always returns true.
/// </para>
/// <example>
/// Simple condition example:
/// <code>
/// LSProcessNodeCondition userTypeCheck = (process, node) => 
///     process.Data.ContainsKey("UserType") &amp;&amp; 
///     process.Data["UserType"].ToString() == "Premium";
/// </code>
/// 
/// Composite condition example:
/// <code>
/// LSProcessNodeCondition timeCheck = (process, node) => 
///     DateTime.Now.Hour >= 9 &amp;&amp; DateTime.Now.Hour &lt;= 17;
/// </code>
/// 
/// Combining conditions:
/// <code>
/// LSProcessNodeCondition combined = userTypeCheck + timeCheck;
/// 
/// // Usage in node creation
/// builder.Handler("premium-handler", PremiumLogic)
///        .WithCondition(combined);
/// </code>
/// </example>
/// </remarks>
public delegate bool LSProcessNodeCondition(ILSProcess process, ILSProcessNode node);

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
public static class LSProcessConditions {
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

        try {
            foreach (LSProcessNodeCondition condition in node.Conditions.GetInvocationList()) {
                if (!condition(process, node)) return false;
            }
            return true;
        } catch {
            // Any exception in condition evaluation is treated as condition failure
            return false;
        }
    }
}
