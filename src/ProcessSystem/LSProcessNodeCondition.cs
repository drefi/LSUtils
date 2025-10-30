namespace LSUtils.ProcessSystem;
/// <summary>
/// Delegate for conditional node execution based on process state and node context.
/// <para>
/// LSProcessNodeCondition provides the foundation for implementing conditional execution
/// within the processing system. Multiple conditions can be composed using delegate
/// composition (+=) to create complex conditional logic, where ALL conditions must
/// return true for the node to be eligible for processing.
/// </para>
/// <para>
/// <b>Evaluation Strategy:</b><br/>
/// - Short-circuit evaluation: stops at the first failing condition<br/>
/// - AND logic: all conditions in the chain must pass<br/>
/// - Exception safety: thrown exceptions are treated as failed conditions<br/>
/// - Null handling: nodes without conditions are always eligible
/// </para>
/// <para>
/// <b>Performance Guidelines:</b><br/>
/// Conditions should be lightweight, deterministic functions without side effects.
/// They are evaluated before every processing cycle and should return consistent
/// results for the same inputs.
/// </para>
/// </summary>
/// <param name="process">Process instance containing data and context for evaluation.</param>
/// <param name="node">Node being evaluated, providing access to metadata and configuration.</param>
/// <returns>True if the condition is satisfied and the node should be processed, false otherwise.</returns>
/// <example>
/// Common condition patterns:
/// <code>
/// // Data presence check
/// LSProcessNodeCondition hasUserData = (process, node) => 
///     process.TryGetData&lt;string&gt;("userId", out _);
///
/// // Business logic condition  
/// LSProcessNodeCondition isPremiumUser = (process, node) =>
///     process.TryGetData&lt;string&gt;("userType", out var type) &amp;&amp; type == "Premium";
///
/// // Time-based condition
/// LSProcessNodeCondition isBusinessHours = (process, node) => {
///     var now = DateTime.Now;
///     return now.Hour >= 9 &amp;&amp; now.Hour &lt;= 17 &amp;&amp; now.DayOfWeek != DayOfWeek.Weekend;
/// };
///
/// // Combining conditions (ALL must pass)
/// LSProcessNodeCondition combined = hasUserData + isPremiumUser + isBusinessHours;
/// 
/// // Usage in builder
/// builder.Handler("premium-handler", PremiumHandler, conditions: combined);
/// </code>
/// </example>
public delegate bool LSProcessNodeCondition(LSProcess process, ILSProcessNode node);
