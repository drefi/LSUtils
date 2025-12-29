namespace LSUtils.ProcessSystem;
/// <summary>
/// Delegate for nested process tree construction within parent contexts.
/// <para>
/// LSProcessBuilderAction enables hierarchical process tree construction through nested lambda
/// expressions. Each invocation receives a scoped builder instance for the current context,
/// allowing clean separation of concerns while maintaining fluent method chaining across
/// nested levels of the hierarchy.
/// </para>
/// <para>
/// <b>Scoped Building Pattern:</b><br/>
/// - Isolated context: subBuilder operates on the current node's children<br/>
/// - Automatic integration: results merge seamlessly into parent hierarchy<br/>
/// - Unlimited nesting: supports arbitrarily deep hierarchical structures<br/>
/// - Type safety: consistent builder interface across all nesting levels
/// </para>
/// <para>
/// <b>Common Patterns:</b><br/>
/// Used extensively in Sequence(), Selector(), Parallel(), and Inverter() methods
/// to enable declarative construction of complex processing workflows with readable,
/// maintainable code structure.
/// </para>
/// </summary>
/// <param name="subBuilder">Scoped builder instance for constructing nested children within the current node context.</param>
/// <returns>The builder instance after nested construction, enabling continued method chaining.</returns>
/// <example>
/// Typical usage patterns:
/// <code>
/// // Nested sequence with selector fallback
/// builder.Sequence("main-process", seq => seq
///     .Handler("validate", ValidateHandler)
///     .Selector("execution-strategy", sel => sel
///         .Handler("primary", PrimaryHandler)
///         .Handler("fallback", FallbackHandler))
///     .Handler("cleanup", CleanupHandler));
///
/// // Parallel processing with nested workflows  
/// builder.Parallel("concurrent-tasks", par => par
///     .Sequence("data-pipeline", data => data
///         .Handler("load", LoadDataHandler)
///         .Handler("transform", TransformHandler))
///     .Sequence("notification-pipeline", notif => notif
///         .Handler("prepare", PrepareNotificationHandler)
///         .Handler("send", SendNotificationHandler)));
/// </code>
/// </example>
public delegate LSProcessTreeBuilder LSProcessBuilderAction(LSProcessTreeBuilder subBuilder);
