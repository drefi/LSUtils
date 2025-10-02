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
