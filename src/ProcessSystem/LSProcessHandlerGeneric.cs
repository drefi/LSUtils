namespace LSUtils.ProcessSystem;

/// <summary>
/// Generic version of LSProcessHandler that provides strongly-typed access to the process instance.
/// Eliminates the need for casting in handlers when working with specific process types.
/// </summary>
/// <typeparam name="TProcess">The specific process type this handler operates on.</typeparam>
/// <param name="session">Processing session with strongly-typed process access.</param>
/// <returns>Execution status indicating the outcome and controlling subsequent processing flow.</returns>
/// <example>
/// Usage with strongly-typed handlers:
/// <code>
/// // Handler with strong typing - no casting needed
/// LSProcessHandler&lt;EngageTask&gt; stronglyTypedHandler = (session) => {
///     // session.Process is already EngageTask, no casting needed
///     if (!session.Process.Entity.CanAttack) {
///         return LSProcessResultStatus.FAILURE;
///     }
///     session.Process.Entity.Attack(session.Process.Target);
///     return LSProcessResultStatus.SUCCESS;
/// };
///
/// // Usage in builder
/// builder.Handler("attack", stronglyTypedHandler);
/// </code>
/// </example>
public delegate LSProcessResultStatus LSProcessHandler<TProcess>(LSProcessSession<TProcess> session)
    where TProcess : LSProcess;
