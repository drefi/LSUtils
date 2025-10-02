namespace LSUtils.Processing;
/// <summary>
/// Delegate that represents a process handler function in the LSProcessing system.
/// Encapsulates the business logic to be executed when a process is executed within the processing pipeline.
/// </summary>
/// <param name="process">The process being executed, containing all relevant process data and context.</param>
/// <param name="session">The processing session executing this handler, providing access to session metadata and state.</param>
/// <returns>Processing status indicating the outcome of the handler execution.</returns>
/// <remarks>
/// <para><strong>Handler Function Design:</strong></para>
/// <para>This delegate serves as the core execution unit within the LSProcessing system, providing a standardized
/// interface for implementing business logic that can be composed into complex processing pipelines through
/// the hierarchical node structure.</para>
/// 
/// <para><strong>Implementation Guidelines:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Stateless Design</strong>: Handlers should be stateless and idempotent when possible to ensure predictable behavior</description></item>
/// <item><description><strong>Exception Safety</strong>: Handlers should handle exceptions gracefully and return appropriate status codes</description></item>
/// <item><description><strong>Performance Considerations</strong>: Handlers should be reasonably fast to avoid blocking the processing pipeline</description></item>
/// <item><description><strong>Session Access</strong>: Use the session parameter to access NodeID, Priority, Conditions, and ExecutionCount</description></item>
/// <item><description><strong>Data Access</strong>: Access process-specific data and context through the process parameter</description></item>
/// </list>
/// 
/// <para><strong>Return Value Semantics:</strong></para>
/// <list type="bullet">
/// <item><description><strong>SUCCESS</strong>: Handler completed successfully, processing can continue</description></item>
/// <item><description><strong>FAILURE</strong>: Handler encountered an error or business rule violation</description></item>
/// <item><description><strong>WAITING</strong>: Handler initiated asynchronous operation, requires external Resume/Fail</description></item>
/// <item><description><strong>CANCELLED</strong>: Handler was cancelled or determined processing should stop</description></item>
/// </list>
/// 
/// <para><strong>Execution Context:</strong></para>
/// <para>Handlers are executed within LSProcessHandlerNode and have access to:</para>
/// <list type="bullet">
/// <item><description>Process data and context through the process parameter</description></item>
/// <item><description>Session metadata (ID, priority, conditions) through the session parameter</description></item>
/// <item><description>Execution statistics through session.ExecutionCount (shared across node clones)</description></item>
/// <item><description>Processing pipeline state and hierarchy navigation capabilities</description></item>
/// </list>
/// 
/// <para><strong>Usage Patterns:</strong></para>
/// <code>
/// // Simple synchronous handler
/// LSProcessHandler simpleHandler = (process, session) => {
///     // Perform business logic
///     if (BusinessRule.IsValid(process.Data))
///         return LSProcessResultStatus.SUCCESS;
///     return LSProcessResultStatus.FAILURE;
/// };
/// 
/// // Asynchronous handler with WAITING status
/// LSProcessHandler asyncHandler = (process, session) => {
///     if (!asyncOperation.IsStarted) {
///         asyncOperation.StartAsync(process.Data);
///         return LSProcessResultStatus.WAITING;
///     }
///     // This will be called again when Resume() is invoked
///     return asyncOperation.IsComplete ? 
///         LSProcessResultStatus.SUCCESS : LSProcessResultStatus.WAITING;
/// };
/// </code>
/// 
/// <para><strong>Integration Benefits:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Composability</strong>: Handlers can be easily combined into complex processing trees</description></item>
/// <item><description><strong>Testability</strong>: Individual handlers can be unit tested in isolation</description></item>
/// <item><description><strong>Reusability</strong>: Handlers can be reused across different processing contexts</description></item>
/// <item><description><strong>Maintainability</strong>: Business logic is encapsulated in focused, single-responsibility functions</description></item>
/// </list>
/// </remarks>
public delegate LSProcessResultStatus LSProcessHandler(ILSProcess process, LSProcessSession session);
