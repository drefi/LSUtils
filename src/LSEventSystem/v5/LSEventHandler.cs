namespace LSUtils.EventSystem;

/// <summary>
/// Delegate that represents an event handler function in the LSEventSystem v5.
/// Encapsulates the business logic to be executed when an event is processed.
/// </summary>
/// <param name="event">The event being processed, containing all relevant event data and context.</param>
/// <param name="node">The handler node executing this delegate, providing access to node metadata and state.</param>
/// <returns>Processing status indicating the outcome of the handler execution.</returns>
/// <remarks>
/// <para><strong>Implementation Guidelines:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Stateless</strong>: Handlers should be stateless and idempotent when possible</description></item>
/// <item><description><strong>Exception Handling</strong>: Handlers should handle exceptions gracefully and return appropriate status</description></item>
/// <item><description><strong>Performance</strong>: Handlers should be reasonably fast to avoid blocking the processing pipeline</description></item>
/// <item><description><strong>Context Access</strong>: Use the node parameter to access NodeID, Priority, Conditions, and ExecutionCount</description></item>
/// </list>
/// 
/// <para><strong>Return Value Semantics:</strong></para>
/// <list type="bullet">
/// <item><description><strong>SUCCESS</strong>: Handler completed successfully</description></item>
/// <item><description><strong>FAILURE</strong>: Handler encountered an error or business rule violation</description></item>
/// <item><description><strong>WAITING</strong>: Handler initiated asynchronous operation, requires external Resume/Fail</description></item>
/// <item><description><strong>CANCELLED</strong>: Handler was cancelled or determined processing should stop</description></item>
/// </list>
/// 
/// <para><strong>Execution Context:</strong></para>
/// <para>Handlers are executed within LSEventHandlerNode.Process() and have access to:</para>
/// <list type="bullet">
/// <item><description>Event data through the event parameter</description></item>
/// <item><description>Node metadata (ID, priority, conditions) through the node parameter</description></item>
/// <item><description>Execution statistics through node.ExecutionCount (shared across clones)</description></item>
/// </list>
/// </remarks>
public delegate LSEventProcessStatus LSEventHandler(LSEvent @event, ILSEventNode node);
