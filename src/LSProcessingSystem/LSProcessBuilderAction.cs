namespace LSUtils.Processing;

/// <summary>
/// Delegate type for process tree building operations that enable nested hierarchy construction.
/// Provides a fluent interface for building complex processing workflows with hierarchical node structures.
/// </summary>
/// <param name="subBuilder">A tree builder instance initialized for nested operations within a parent context.</param>
/// <returns>The tree builder instance after performing nested operations, enabling method chaining.</returns>
/// <remarks>
/// <para><strong>Builder Pattern Integration:</strong></para>
/// <para>This delegate serves as the foundation for the fluent builder pattern used throughout</para>
/// <para>the LSProcessing system. It enables declarative construction of complex processing</para>
/// <para>hierarchies using nested lambda expressions and method chaining.</para>
/// 
/// <para><strong>Hierarchical Construction Pattern:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Isolated Building</strong>: The subBuilder operates within its own scope for clean separation</description></item>
/// <item><description><strong>Fluent Operations</strong>: Enables continuous method chaining within nested contexts</description></item>
/// <item><description><strong>Automatic Integration</strong>: Results are seamlessly integrated into the parent hierarchy</description></item>
/// <item><description><strong>Type Safety</strong>: Maintains consistent builder types throughout all nesting levels</description></item>
/// <item><description><strong>Composition Support</strong>: Allows unlimited nesting depth for complex workflows</description></item>
/// </list>
/// 
/// <para><strong>Usage Patterns:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Sequential Processing</strong>: Build sequences where operations must complete in order</description></item>
/// <item><description><strong>Conditional Logic</strong>: Create selectors that choose between alternative processing paths</description></item>
/// <item><description><strong>Parallel Execution</strong>: Define parallel operations with threshold-based completion</description></item>
/// <item><description><strong>Mixed Hierarchies</strong>: Combine different node types for sophisticated workflows</description></item>
/// </list>
/// 
/// <para><strong>Common Usage Examples:</strong></para>
/// <code>
/// // Sequential processing with nested handlers
/// builder.Sequence("validation", sub => sub
///     .Execute("validateUser", userValidator)
///     .Execute("validateData", dataValidator)
///     .Selector("fallback", sel => sel
///         .Execute("primaryHandler", primaryHandler)
///         .Execute("fallbackHandler", fallbackHandler)));
/// 
/// // Parallel processing with multiple workflows
/// builder.Parallel("concurrent", sub => sub
///     .Sequence("workflow1", w1 => w1.Execute("step1", handler1))
///     .Sequence("workflow2", w2 => w2.Execute("step2", handler2)));
/// </code>
/// 
/// <para><strong>Design Benefits:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Readability</strong>: Declarative syntax makes complex workflows easy to understand</description></item>
/// <item><description><strong>Maintainability</strong>: Clear structure simplifies modifications and debugging</description></item>
/// <item><description><strong>Reusability</strong>: Common patterns can be extracted as reusable functions</description></item>
/// <item><description><strong>Composability</strong>: Small building blocks combine to create complex behaviors</description></item>
/// </list>
/// </remarks>
public delegate LSProcessTreeBuilder LSProcessBuilderAction(LSProcessTreeBuilder subBuilder);
