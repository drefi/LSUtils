namespace LSUtils.EventSystem;

/// <summary>
/// Delegate type for sub-context building operations that allow nested hierarchy construction.
/// </summary>
/// <param name="subBuilder">A builder instance initialized with the parent node for nested operations.</param>
/// <returns>The builder instance after performing nested operations for method chaining.</returns>
/// <remarks>
/// <para><strong>Sub-Context Pattern:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Isolated Building</strong>: The subBuilder operates on a cloned copy of the parent node</description></item>
/// <item><description><strong>Fluent Operations</strong>: Allows method chaining within the sub-context scope</description></item>
/// <item><description><strong>Automatic Integration</strong>: Results are automatically merged back into the parent hierarchy</description></item>
/// <item><description><strong>Type Safety</strong>: Ensures consistent builder pattern throughout nested operations</description></item>
/// </list>
/// 
/// <para><strong>Usage Example:</strong></para>
/// <code>
/// builder.Sequence("parentSeq", sub => sub
///     .Execute("handler1", handler1)
///     .Execute("handler2", handler2))
/// </code>
/// </remarks>
public delegate LSEventContextBuilder LSEventSubContextBuilder(LSEventContextBuilder subBuilder);
