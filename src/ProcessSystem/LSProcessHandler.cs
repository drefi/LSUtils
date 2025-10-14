namespace LSUtils.ProcessSystem;
/// <summary>
/// Delegate representing executable business logic within the LSProcessing system.
/// <para>
/// LSProcessHandler encapsulates the core execution units that perform actual business
/// operations within the processing pipeline. Handlers receive execution context through
/// LSProcessSession and return status codes to control processing flow. They serve as
/// the bridge between the abstract processing tree structure and concrete business logic.
/// </para>
/// <para>
/// <b>Design Guidelines:</b><br/>
/// - Stateless and idempotent when possible for predictable behavior<br/>
/// - Exception-safe with graceful error handling and appropriate status returns<br/>
/// - Performance-conscious to avoid blocking the processing pipeline<br/>
/// - Pure business logic focused on single responsibility
/// </para>
/// <para>
/// <b>Session Context:</b><br/>
/// Handlers access process data, node metadata, and execution statistics through the
/// session parameter. This provides access to process state, execution count (shared
/// across clones), and contextual information for conditional logic.
/// </para>
/// <para>
/// <b>Status Semantics:</b><br/>
/// Return values control processing flow - SUCCESS continues processing, FAILURE
/// may terminate sequences, WAITING requires external Resume/Fail, and CANCELLED
/// stops processing entirely.
/// </para>
/// </summary>
/// <param name="session">Processing session providing access to process data, node context, and execution statistics.</param>
/// <returns>Execution status indicating the outcome and controlling subsequent processing flow.</returns>
/// <example>
/// Common handler patterns:
/// <code>
/// // Simple validation handler
/// LSProcessHandler validateInput = (session) => {
///     if (!session.Process.TryGetData&lt;string&gt;("input", out var input) || string.IsNullOrEmpty(input)) {
///         session.Process.SetData("error", "Input is required");
///         return LSProcessResultStatus.FAILURE;
///     }
///     return LSProcessResultStatus.SUCCESS;
/// };
///
/// // Async operation handler
/// LSProcessHandler asyncOperation = (session) => {
///     var taskId = StartAsyncTask(session.Process.GetData&lt;object&gt;("taskData"));
///     session.Process.SetData("asyncTaskId", taskId);
///     return LSProcessResultStatus.WAITING; // Will be resumed when task completes
/// };
///
/// // Conditional business logic
/// LSProcessHandler conditionalLogic = (session) => {
///     var userType = session.Process.GetData&lt;string&gt;("userType");
///     if (userType == "Premium") {
///         return ProcessPremiumUser(session);
///     } else {
///         return ProcessStandardUser(session);
///     }
/// };
/// </code>
/// </example>
public delegate LSProcessResultStatus LSProcessHandler(LSProcessSession session);
