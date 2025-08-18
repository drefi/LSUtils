namespace LSUtils.EventSystem;

/// <summary>
/// LSUtils /// ### 2. Event-Scoped Callback Builders
/// ```csharp
/// // Inline processing with automatic cleanup
/// var success = myEvent.ProcessWith<MyEvent>(dispa/// ### Performance Monitoring
/// ```csharp
/// orderEvent.ProcessWith<OrderEvent>(dispatcher, builder => builder
///     .OnExecution((evt, ctx) => ProcessOrder(evt))
///     .OnSlowProcessing(TimeSpan.FromSeconds(5), (evt, ctx) => LogSlowOrder(evt))
///     .MeasureExecutionTime((evt, time) => RecordMetrics(evt, time))
/// );
/// ```
/// 
/// ### High-Performance Event Processing (Batching Optimization)
/// ```csharp
/// // This example shows how batching optimization improves performance
/// // All 8 handler registrations below are internally collected and registered as 1 batch
/// complexEvent.ProcessWith<ComplexBusinessEvent>(dispatcher, builder => builder
///     .OnCriticalValidation((evt, ctx) => ValidateBusinessRules(evt))
///     .OnValidation((evt, ctx) => ValidateInputs(evt))
///     .OnPrepare((evt, ctx) => SetupResources(evt))
///     .OnHighPriorityExecution((evt, ctx) => ExecuteCore(evt))
///     .OnExecution((evt, ctx) => ExecuteSecondary(evt))
///     .OnFinalize((evt, ctx) => CleanupResources(evt))
///     .OnSuccess((evt, ctx) => LogSuccess(evt))
///     .OnError((evt, ctx) => HandleErrors(evt))
/// );
/// // Internal optimization: 8 handlers → 1 registration → automatic cleanup
/// // Performance benefit: ~8x reduction in registration overhead
/// ```uilder => builder
///     .OnValidation((evt, ctx) => ValidateEvent(evt))
///     .OnExecution((evt, ctx) => ProcessEvent(evt))
///     .OnSuccess((evt, ctx) => LogSuccess(evt))
///     .OnError((evt, ctx) => HandleError(evt))
/// );
/// 
/// // Manual lifecycle management
/// var builder = myEvent.RegisterCallback<MyEvent>(dispatcher);
/// builder.OnValidation(ValidateHandler)
///        .OnExecution(ExecuteHandler);
/// var success = builder.ProcessAndCleanup();
/// ```
/// 
/// **Performance Optimization**: The callback builder system now uses internal batching
/// optimization. Instead of registering each handler individually (which could create
/// N separate registrations per event), all handlers are collected into a single batch
/// and registered as one composite handler. This provides:
/// - Reduced registration overhead from O(N) to O(1) per event
/// - Lower memory pressure and fewer delegate allocations
/// - Automatic cleanup without manual handler tracking
/// - Phase-aware execution that only runs relevant handlers per phase
/// 
/// The optimization is completely transparent - the API remains identical but performance
/// is dramatically improved, especially for events with many handlers.- Comprehensive Documentation
/// 
/// The LSUtils Event System provides a clean, phase-based approach to event processing
/// with support for both traditional global handlers and modern event-scoped callback builders.
/// 
/// ## Core Components
/// 
/// ### 1. Events
/// - **ILSEvent**: Core event interface defining the contract for all events
/// - **LSBaseEvent**: Abstract base class for events without specific source instances
/// - **LSEvent&lt;T&gt;**: Generic base class for events tied to specific object instances
/// 
/// ### 2. Event Processing
/// - **LSDispatcher**: Central coordinator that processes events through phases
/// - **LSEventPhase**: Enum defining the five processing phases (VALIDATE, PREPARE, EXECUTE, FINALIZE, COMPLETE)
/// - **LSPhasePriority**: Enum defining execution priorities within phases
/// - **LSPhaseResult**: Enum defining how handlers control event flow
/// 
/// ### 3. Handler Registration
/// - **LSEventRegistration&lt;T&gt;**: Fluent builder for global handler registration
/// - **LSEventCallbackBuilder&lt;T&gt;**: Fluent builder for event-scoped handlers (internally optimized with batching)
/// - **LSEventCallbackBatch&lt;T&gt;**: Internal batching structure for optimized handler collection
/// - **LSPhaseHandler&lt;T&gt;**: Delegate type for event handler functions
/// 
/// ### 4. Context and Metadata
/// - **LSPhaseContext**: Execution context provided to handlers
/// - **LSHandlerRegistration**: Internal registration data (not user-facing)
/// 
/// ## Processing Flow
/// 
/// Events are processed through five distinct phases in order:
/// 
/// 1. **VALIDATE**: Input validation, permission checks, early validation logic
/// 2. **PREPARE**: Setup, resource allocation, preparation logic
/// 3. **EXECUTE**: Core business logic and main event processing
/// 4. **FINALIZE**: Cleanup, side effects, post-processing logic
/// 5. **COMPLETE**: Always runs regardless of previous results (logging, metrics, etc.)
/// 
/// Within each phase, handlers execute in priority order:
/// - CRITICAL (0) - System-critical operations
/// - HIGH (1) - Important business logic
/// - NORMAL (2) - Standard operations (default)
/// - LOW (3) - Nice-to-have features
/// - BACKGROUND (4) - Background operations
/// 
/// ## Handler Registration Approaches
/// 
/// ### 1. Global Handler Registration
/// ```csharp
/// // Register a global handler that responds to all events of this type
/// dispatcher.For&lt;UserRegistrationEvent&gt;()
///     .InPhase(LSEventPhase.VALIDATE)
///     .WithPriority(LSPhasePriority.HIGH)
///     .ForInstance(specificUser)  // Optional: restrict to specific instance
///     .MaxExecutions(5)           // Optional: limit executions
///     .When(evt => evt.IsActive)  // Optional: add condition
///     .Register(myHandler);
/// ```
/// 
/// ### 2. Event-Scoped Callback Builders
/// ```csharp
/// // Inline processing with automatic cleanup
/// var success = myEvent.ProcessWith&lt;MyEvent&gt;(dispatcher, builder => builder
///     .OnValidation((evt, ctx) => ValidateEvent(evt))
///     .OnExecution((evt, ctx) => ProcessEvent(evt))
///     .OnSuccess((evt, ctx) => LogSuccess(evt))
///     .OnError((evt, ctx) => HandleError(evt))
/// );
/// 
/// // Manual lifecycle management
/// var builder = myEvent.RegisterCallback&lt;MyEvent&gt;(dispatcher);
/// builder.OnValidation(ValidateHandler)
///        .OnExecution(ExecuteHandler);
/// var success = builder.ProcessAndCleanup();
/// ```
/// 
/// ## Callback Builder Methods
/// 
/// The LSEventCallbackBuilder provides over 30 specialized methods organized into categories:
/// 
/// ### Core Phase Methods
/// - `OnValidation()`, `OnPrepare()`, `OnExecution()`, `OnFinalize()`, `OnComplete()`
/// - `OnCriticalValidation()`, `OnHighPriorityExecution()` (priority variants)
/// 
/// ### Conditional Methods
/// - `OnSuccess()`, `OnError()`, `OnCancel()` (state-based)
/// - `OnDataPresent()`, `OnDataEquals()` (data-based)
/// - `OnTimeout()`, `OnSlowProcessing()` (timing-based)
/// 
/// ### Action Shortcuts
/// - `DoOnValidation()`, `DoOnExecution()`, `DoOnComplete()` (simple actions)
/// - `DoWhen()` (conditional actions)
/// 
/// ### Data Manipulation
/// - `SetData()`, `SetDataWhen()`, `TransformData()`
/// - `ValidateData()` (with automatic error handling)
/// 
/// ### Logging and Monitoring
/// - `LogPhase()`, `LogOnError()`, `MeasureExecutionTime()`
/// 
/// ### Error Handling
/// - `CancelIf()`, `RetryOnError()`
/// 
/// ### Composition
/// - `Then()`, `If()`, `InParallel()`
/// 
/// ## Key Features
/// 
/// ### Thread Safety
/// All components are thread-safe and can be used in concurrent environments.
/// 
/// ### Instance Isolation
/// Event-scoped handlers only execute for their specific event instance,
/// providing complete isolation between different event instances.
/// 
/// ### Automatic Cleanup
/// Event-scoped handlers are automatically cleaned up after processing,
/// preventing memory leaks and handler accumulation. The system uses an
/// optimized batching approach where multiple handlers are registered as
/// a single composite handler with automatic cleanup through one-time execution.
/// 
/// ### Performance Optimization
/// The callback builder system employs internal batching optimization:
/// - **Batched Registration**: Multiple handlers are collected and registered as one unit
/// - **Reduced Overhead**: Registration complexity reduced from O(N) to O(1) per event  
/// - **Memory Efficiency**: Eliminates individual handler tracking and cleanup loops
/// - **Phase-Aware Execution**: Only executes handlers relevant to the current phase
/// - **Transparent Optimization**: No API changes required - existing code benefits immediately
/// 
/// ### Type Safety
/// The system provides compile-time type checking for all event types
/// and handler signatures.
/// 
/// ### Error Handling
/// Comprehensive error handling with data-based error storage,
/// automatic retry mechanisms, and graceful degradation.
/// 
/// ### Performance Monitoring
/// Built-in timing and performance monitoring capabilities
/// with context-aware execution metrics.
/// 
/// ## Internal Architecture & Optimization
/// 
/// ### Batching Optimization
/// The event system implements an advanced batching optimization for event-scoped handlers:
/// 
/// **LSEventCallbackBatch&lt;TEvent&gt;**: Internal structure that collects handlers before registration
/// - Organizes handlers by phase (VALIDATE, PREPARE, EXECUTE, FINALIZE, COMPLETE)
/// - Supports priority ordering and conditional execution
/// - Provides phase-aware handler retrieval for efficient execution
/// 
/// **Enhanced LSDispatcher**: Supports batch registration and execution
/// - `RegisterBatchedHandlers()`: Registers entire batch as single composite handler
/// - `ExecuteBatchedHandlers()`: Phase-aware execution of batched handlers
/// - Automatic cleanup through MaxExecutions = 1 constraint
/// 
/// **Benefits**:
/// - **Before Optimization**: N separate registrations with individual cleanup loops
/// - **After Optimization**: 1 registration with automatic cleanup
/// - **Memory Impact**: Reduced delegate allocations and closure overhead
/// - **Performance Impact**: O(N) to O(1) registration complexity per event
/// 
/// ### Thread Safety
/// All optimizations maintain the existing thread-safety guarantees:
/// - Concurrent handler registration and execution
/// - Thread-safe batch collection and registration
/// - Lock-based protection for shared data structures
/// 
/// ## Usage Patterns
/// 
/// ### Simple Event Processing
/// ```csharp
/// var event = new SystemStartupEvent("1.0.0");
/// var success = event.ProcessWith&lt;SystemStartupEvent&gt;(dispatcher, builder => builder
///     .OnValidation((evt, ctx) => ValidateSystem())
///     .OnExecution((evt, ctx) => StartServices())
///     .OnComplete((evt, ctx) => LogStartup())
/// );
/// ```
/// 
/// ### Complex Validation Pipeline
/// ```csharp
/// userEvent.ProcessWith&lt;UserRegistrationEvent&gt;(dispatcher, builder => builder
///     .ValidateData&lt;string&gt;("email", email => IsValidEmail(email), "Invalid email")
///     .ValidateData&lt;int&gt;("age", age => age >= 18, "Must be 18 or older")
///     .OnSuccess((evt, ctx) => CreateUser(evt))
///     .OnError((evt, ctx) => SendValidationError(evt))
/// );
/// ```
/// 
/// ### Performance Monitoring
/// ```csharp
/// orderEvent.ProcessWith&lt;OrderEvent&gt;(dispatcher, builder => builder
///     .OnExecution((evt, ctx) => ProcessOrder(evt))
///     .OnSlowProcessing(TimeSpan.FromSeconds(5), (evt, ctx) => LogSlowOrder(evt))
///     .MeasureExecutionTime((evt, time) => RecordMetrics(evt, time))
/// );
/// ```
/// 
/// ### Conditional Processing
/// ```csharp
/// paymentEvent.ProcessWith&lt;PaymentEvent&gt;(dispatcher, builder => builder
///     .If(evt => evt.Amount > 1000, premiumBuilder => premiumBuilder
///         .OnValidation((evt, ctx) => ValidatePremiumPayment(evt))
///         .OnExecution((evt, ctx) => ProcessPremiumPayment(evt))
///     )
///     .OnDataEquals("payment.type", "credit", (evt, ctx) => ProcessCreditPayment(evt))
///     .OnTimeout(TimeSpan.FromSeconds(30), (evt, ctx) => HandleTimeout(evt))
/// );
/// ```
/// 
/// ## Best Practices
/// 
/// 1. **Use event-scoped builders for event-specific logic**
/// 2. **Use global handlers for cross-cutting concerns**
/// 3. **Keep handlers focused and single-purpose**
/// 4. **Use appropriate phases for different types of logic**
/// 5. **Leverage data storage for inter-handler communication**
/// 6. **Use conditional methods for clean branching logic**
/// 7. **Monitor performance with built-in timing methods**
/// 8. **Handle errors gracefully with error-specific handlers**
/// 9. **Use validation methods for data integrity**
/// 10. **Take advantage of automatic cleanup for one-time handlers**
/// 11. **Prefer event-scoped builders over global handlers for better performance** (batching optimization)
/// 12. **Chain multiple handlers in single builder call** (maximizes batching benefits)
/// 
/// ## Performance Considerations
/// 
/// - **Event-Scoped Builders**: Internally optimized with batching - prefer for complex event processing
/// - **Global Handlers**: Best for cross-cutting concerns that apply to many events
/// - **Handler Chaining**: Multiple handlers in one builder call = single optimized registration
/// - **Memory Efficiency**: Automatic cleanup eliminates manual handler lifecycle management
/// - **Phase Awareness**: System only executes handlers relevant to current processing phase
/// </summary>
internal static class EventSystemDocumentation
{
    // This is a documentation-only class - no implementation needed
}
