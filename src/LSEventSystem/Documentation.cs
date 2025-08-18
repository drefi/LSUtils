namespace LSUtils.EventSystem;

/// <summary>
/// LSUtils Event System - Comprehensive Documentation
/// 
/// The LSUtils Event System provides a clean, phase-based approach to event processing
/// with support for both traditional global handlers and modern event-scoped callback builders.
/// 
/// ## Core Event Types
/// 
/// ### ILSEvent Interface
/// ```csharp
/// public interface ILSEvent {
///     Guid Id { get; }
///     Type EventType { get; }
///     DateTime CreatedAt { get; }
///     bool IsCancelled { get; }
///     bool IsCompleted { get; }
///     LSEventPhase CurrentPhase { get; }
///     LSEventPhase CompletedPhases { get; }
///     string? ErrorMessage { get; }
///     IReadOnlyDictionary<string, object> Data { get; }
///     T GetData<T>(string key);
///     bool TryGetData<T>(string key, out T value);
/// }
/// ```
/// 
/// ### ILSMutableEvent Interface (Internal)
/// ```csharp
/// internal interface ILSMutableEvent : ILSEvent {
///     new bool IsCancelled { get; set; }
///     new bool IsCompleted { get; set; }
///     new LSEventPhase CurrentPhase { get; set; }
///     new LSEventPhase CompletedPhases { get; set; }
///     new string? ErrorMessage { get; set; }
///     void SetData(string key, object value);
///     void SetErrorMessage(string message);
/// }
/// ```
/// 
/// ### LSBaseEvent Abstract Class
/// ```csharp
/// public abstract class LSBaseEvent : ILSMutableEvent {
///     // Core properties (auto-generated)
///     public Guid Id { get; }
///     public Type EventType { get; }
///     public DateTime CreatedAt { get; }
///     
///     // State properties
///     public bool IsCancelled { get; set; }
///     public bool IsCompleted { get; set; }
///     public LSEventPhase CurrentPhase { get; set; }
///     public LSEventPhase CompletedPhases { get; set; }
///     public string? ErrorMessage { get; set; }
///     
///     // Data storage
///     public IReadOnlyDictionary<string, object> Data { get; }
///     public void SetData(string key, object value);
///     public T GetData<T>(string key);
///     public bool TryGetData<T>(string key, out T value);
///     
///     // Event processing methods
///     public LSEventCallbackBuilder<TEventType> RegisterCallback<TEventType>(LSDispatcher dispatcher);
///     public bool ProcessWith<TEventType>(LSDispatcher dispatcher, Action<LSEventCallbackBuilder<TEventType>>? configure = null);
///     public bool Process(LSDispatcher dispatcher);
/// }
/// ```
/// 
/// ### LSEvent<TInstance> Generic Class
/// ```csharp
/// public abstract class LSEvent<TInstance> : LSBaseEvent where TInstance : class {
///     public TInstance Instance { get; }
///     protected LSEvent(TInstance instance);
/// }
/// ```
/// 
/// ## Event Phase and Priority Types
/// 
/// ### LSEventPhase Enum
/// ```csharp
/// [Flags]
/// public enum LSEventPhase {
///     VALIDATE = 1,   // Input validation, permission checks
///     PREPARE = 2,    // Setup, resource allocation
///     EXECUTE = 4,    // Core business logic
///     FINALIZE = 8,   // Cleanup, side effects
///     COMPLETE = 16   // Always runs (logging, metrics)
/// }
/// ```
/// 
/// ### LSPhasePriority Enum
/// ```csharp
/// public enum LSPhasePriority {
///     CRITICAL = 0,    // System-critical operations
///     HIGH = 1,        // Important business logic
///     NORMAL = 2,      // Standard operations (default)
///     LOW = 3,         // Nice-to-have features
///     BACKGROUND = 4   // Background operations
/// }
/// ```
/// 
/// ### LSPhaseResult Enum
/// ```csharp
/// public enum LSPhaseResult {
///     CONTINUE,        // Continue to next handler/phase
///     SKIP_REMAINING,  // Skip remaining handlers in current phase
///     CANCEL,          // Cancel event processing
///     RETRY            // Retry current handler (up to 3 times)
/// }
/// ```
/// 
/// ## Handler and Context Types
/// 
/// ### LSPhaseHandler Delegate
/// ```csharp
/// public delegate LSPhaseResult LSPhaseHandler<in TEvent>(TEvent evt, LSPhaseContext context) 
///     where TEvent : ILSEvent;
/// ```
/// 
/// ### LSPhaseContext Class
/// ```csharp
/// public class LSPhaseContext {
///     public LSEventPhase CurrentPhase { get; }
///     public DateTime StartTime { get; }
///     public TimeSpan ElapsedTime { get; }
///     public int HandlerCount { get; }
///     public bool HasErrors { get; }
///     public IReadOnlyList<string> Errors { get; }
/// }
/// ```
/// 
/// ### LSHandlerRegistration Class (Internal)
/// ```csharp
/// internal class LSHandlerRegistration {
///     public Guid Id { get; set; }
///     public Type EventType { get; set; }
///     public Func<ILSEvent, LSPhaseContext, LSPhaseResult> Handler { get; set; }
///     public LSEventPhase Phase { get; set; }
///     public LSPhasePriority Priority { get; set; }
///     public Type? InstanceType { get; set; }
///     public object? Instance { get; set; }
///     public int MaxExecutions { get; set; }
///     public Func<ILSEvent, bool>? Condition { get; set; }
///     public int ExecutionCount { get; set; }
/// }
/// ```
/// 
/// ## Registration and Builder Types
/// 
/// ### LSEventRegistration<TEvent> Class
/// ```csharp
/// public class LSEventRegistration<TEvent> where TEvent : ILSEvent {
///     public LSEventRegistration<TEvent> InPhase(LSEventPhase phase);
///     public LSEventRegistration<TEvent> WithPriority(LSPhasePriority priority);
///     public LSEventRegistration<TEvent> ForInstance<TInstance>(TInstance instance) where TInstance : class;
///     public LSEventRegistration<TEvent> MaxExecutions(int maxExecutions);
///     public LSEventRegistration<TEvent> When(Func<TEvent, bool> condition);
///     public Guid Register(LSPhaseHandler<TEvent> handler);
/// }
/// ```
/// 
/// ### LSEventCallbackBuilder<TEvent> Class
/// ```csharp
/// public class LSEventCallbackBuilder<TEvent> where TEvent : ILSEvent {
///     // Core phase methods
///     public LSEventCallbackBuilder<TEvent> OnValidation(LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnPrepare(LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnExecution(LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnFinalize(LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnComplete(LSPhaseHandler<TEvent> handler);
///     
///     // Priority variants
///     public LSEventCallbackBuilder<TEvent> OnCriticalValidation(LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnHighPriorityExecution(LSPhaseHandler<TEvent> handler);
///     
///     // Conditional methods
///     public LSEventCallbackBuilder<TEvent> OnSuccess(LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnError(LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnCancel(LSPhaseHandler<TEvent> handler);
///     
///     // Data-based methods
///     public LSEventCallbackBuilder<TEvent> OnDataPresent(string key, LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnDataEquals<T>(string key, T value, LSPhaseHandler<TEvent> handler);
///     
///     // Timing methods
///     public LSEventCallbackBuilder<TEvent> OnTimeout(TimeSpan timeout, LSPhaseHandler<TEvent> handler);
///     public LSEventCallbackBuilder<TEvent> OnSlowProcessing(TimeSpan threshold, LSPhaseHandler<TEvent> handler);
///     
///     // Action shortcuts
///     public LSEventCallbackBuilder<TEvent> DoOnValidation(Action<TEvent> action);
///     public LSEventCallbackBuilder<TEvent> DoOnExecution(Action<TEvent> action);
///     public LSEventCallbackBuilder<TEvent> DoOnComplete(Action<TEvent> action);
///     public LSEventCallbackBuilder<TEvent> DoWhen(Func<TEvent, bool> condition, Action<TEvent> action);
///     
///     // Data manipulation
///     public LSEventCallbackBuilder<TEvent> SetData(string key, object value);
///     public LSEventCallbackBuilder<TEvent> SetDataWhen(Func<TEvent, bool> condition, string key, object value);
///     public LSEventCallbackBuilder<TEvent> TransformData<T>(string key, Func<T, T> transform);
///     public LSEventCallbackBuilder<TEvent> ValidateData<T>(string key, Func<T, bool> validator, string errorMessage);
///     
///     // Logging and monitoring
///     public LSEventCallbackBuilder<TEvent> LogPhase(LSEventPhase phase, Action<TEvent, LSPhaseContext> logger);
///     public LSEventCallbackBuilder<TEvent> LogOnError(Action<TEvent, string> logger);
///     public LSEventCallbackBuilder<TEvent> MeasureExecutionTime(Action<TEvent, TimeSpan> onComplete);
///     
///     // Error handling
///     public LSEventCallbackBuilder<TEvent> CancelIf(Func<TEvent, bool> condition);
///     public LSEventCallbackBuilder<TEvent> RetryOnError(int maxRetries);
///     
///     // Composition
///     public LSEventCallbackBuilder<TEvent> Then(Action<LSEventCallbackBuilder<TEvent>> configure);
///     public LSEventCallbackBuilder<TEvent> If(Func<TEvent, bool> condition, Action<LSEventCallbackBuilder<TEvent>> configure);
///     public LSEventCallbackBuilder<TEvent> InParallel(Action<LSEventCallbackBuilder<TEvent>> configure);
///     
///     // Processing
///     public bool ProcessAndCleanup();
/// }
/// ```
/// 
/// ### LSEventCallbackBatch<TEvent> Class (Internal)
/// ```csharp
/// internal class LSEventCallbackBatch<TEvent> where TEvent : ILSEvent {
///     public TEvent TargetEvent { get; }
///     public Dictionary<LSEventPhase, List<BatchedHandler>> HandlersByPhase { get; }
///     public IEnumerable<BatchedHandler> GetHandlersForPhase(LSEventPhase phase);
///     public void AddHandler(LSEventPhase phase, LSPhasePriority priority, LSPhaseHandler<TEvent> handler, Func<TEvent, bool>? condition = null);
/// }
/// 
/// internal class BatchedHandler {
///     public LSPhasePriority Priority { get; set; }
///     public LSPhaseHandler<TEvent> Handler { get; set; }
///     public Func<TEvent, bool>? Condition { get; set; }
/// }
/// ```
/// 
/// ## Event Examples
/// 
/// ### Simple Event (LSBaseEvent)
/// ```csharp
/// public class SystemStartupEvent : LSBaseEvent {
///     public string Version { get; }
///     public DateTime StartupTime { get; }
///     public Dictionary<string, object> Configuration { get; }
///     
///     public SystemStartupEvent(string version, Dictionary<string, object> config) {
///         Version = version;
///         StartupTime = DateTime.UtcNow;
///         Configuration = config;
///         
///         // Store data for handlers
///         SetData("version", version);
///         SetData("config", config);
///     }
/// }
/// ```
/// 
/// ### Typed Event (LSEvent<T>)
/// ```csharp
/// public class UserRegistrationEvent : LSEvent<User> {
///     public string RegistrationSource { get; }
///     public DateTime RegistrationTime { get; }
///     public bool RequiresEmailVerification { get; }
///     
///     public UserRegistrationEvent(User user, string source, bool requiresVerification = true) 
///         : base(user) {
///         RegistrationSource = source;
///         RegistrationTime = DateTime.UtcNow;
///         RequiresEmailVerification = requiresVerification;
///         
///         // Store registration metadata
///         SetData("registration.source", source);
///         SetData("registration.time", RegistrationTime);
///         SetData("verification.required", requiresVerification);
///     }
/// }
/// 
/// public class OrderProcessedEvent : LSEvent<Order> {
///     public decimal ProcessingFee { get; }
///     public PaymentMethod PaymentMethod { get; }
///     public ShippingAddress ShippingAddress { get; }
///     
///     public OrderProcessedEvent(Order order, decimal fee, PaymentMethod payment, ShippingAddress shipping) 
///         : base(order) {
///         ProcessingFee = fee;
///         PaymentMethod = payment;
///         ShippingAddress = shipping;
///         
///         // Store order processing data
///         SetData("processing.fee", fee);
///         SetData("payment.method", payment);
///         SetData("shipping.address", shipping);
///         SetData("order.total", order.Total + fee);
///     }
/// }
/// ```
/// 
/// ## Handler Examples
/// 
/// ### Simple Handler
/// ```csharp
/// public LSPhaseResult ValidateUserRegistration(UserRegistrationEvent evt, LSPhaseContext ctx) {
///     var user = evt.Instance;
///     
///     // Basic validation
///     if (string.IsNullOrEmpty(user.Email)) {
///         evt.SetErrorMessage("Email is required");
///         return LSPhaseResult.CANCEL;
///     }
///     
///     if (user.Age < 18) {
///         evt.SetErrorMessage("User must be 18 or older");
///         return LSPhaseResult.CANCEL;
///     }
///     
///     // Store validation results
///     evt.SetData("validation.passed", true);
///     evt.SetData("validation.timestamp", DateTime.UtcNow);
///     
///     return LSPhaseResult.CONTINUE;
/// }
/// ```
/// 
/// ### Complex Handler with Error Handling
/// ```csharp
/// public LSPhaseResult ProcessOrderPayment(OrderProcessedEvent evt, LSPhaseContext ctx) {
///     var order = evt.Instance;
///     
///     try {
///         // Get payment method from event data
///         if (!evt.TryGetData<PaymentMethod>("payment.method", out var paymentMethod)) {
///             evt.SetErrorMessage("Payment method not specified");
///             return LSPhaseResult.CANCEL;
///         }
///         
///         // Process payment
///         var paymentResult = paymentService.ProcessPayment(order.Total, paymentMethod);
///         
///         if (!paymentResult.Success) {
///             evt.SetErrorMessage($"Payment failed: {paymentResult.ErrorMessage}");
///             evt.SetData("payment.failure.reason", paymentResult.ErrorMessage);
///             evt.SetData("payment.failure.code", paymentResult.ErrorCode);
///             return LSPhaseResult.RETRY; // Retry payment processing
///         }
///         
///         // Store successful payment data
///         evt.SetData("payment.transaction.id", paymentResult.TransactionId);
///         evt.SetData("payment.processed.at", DateTime.UtcNow);
///         evt.SetData("payment.amount", order.Total);
///         
///         return LSPhaseResult.CONTINUE;
///         
///     } catch (PaymentServiceException ex) {
///         evt.SetErrorMessage($"Payment service error: {ex.Message}");
///         evt.SetData("payment.service.error", ex.Message);
///         return LSPhaseResult.RETRY;
///         
///     } catch (Exception ex) {
///         evt.SetErrorMessage($"Unexpected error during payment: {ex.Message}");
///         evt.SetData("payment.unexpected.error", ex.Message);
///         return LSPhaseResult.CANCEL;
///     }
/// }
/// ```
/// 
/// ## Usage Examples
/// 
/// ### Global Handler Registration
/// ```csharp
/// // Register a global validation handler for all user registrations
/// dispatcher.For<UserRegistrationEvent>()
///     .InPhase(LSEventPhase.VALIDATE)
///     .WithPriority(LSPhasePriority.HIGH)
///     .Register((evt, ctx) => {
///         var user = evt.Instance;
///         if (userService.EmailExists(user.Email)) {
///             evt.SetErrorMessage("Email already registered");
///             return LSPhaseResult.CANCEL;
///         }
///         return LSPhaseResult.CONTINUE;
///     });
/// 
/// // Register a handler for specific user instance
/// dispatcher.For<UserRegistrationEvent>()
///     .InPhase(LSEventPhase.EXECUTE)
///     .ForInstance(premiumUser)
///     .Register((evt, ctx) => {
///         // Special processing for premium users
///         userService.AssignPremiumFeatures(evt.Instance);
///         evt.SetData("premium.features.assigned", true);
///         return LSPhaseResult.CONTINUE;
///     });
/// 
/// // Conditional handler registration
/// dispatcher.For<OrderProcessedEvent>()
///     .InPhase(LSEventPhase.FINALIZE)
///     .When(evt => evt.Instance.Total > 1000)
///     .Register((evt, ctx) => {
///         // Special handling for large orders
///         notificationService.NotifyManager(evt.Instance);
///         return LSPhaseResult.CONTINUE;
///     });
/// ```
/// 
/// ### Event-Scoped Processing (Recommended)
/// ```csharp
/// // Simple inline processing
/// var userEvent = new UserRegistrationEvent(newUser, "web");
/// var success = userEvent.ProcessWith<UserRegistrationEvent>(dispatcher, builder => builder
///     .OnValidation((evt, ctx) => {
///         if (string.IsNullOrEmpty(evt.Instance.Email)) {
///             evt.SetErrorMessage("Email is required");
///             return LSPhaseResult.CANCEL;
///         }
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnExecution((evt, ctx) => {
///         userService.CreateUser(evt.Instance);
///         evt.SetData("user.created", true);
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnSuccess((evt, ctx) => {
///         emailService.SendWelcomeEmail(evt.Instance.Email);
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnError((evt, ctx) => {
///         logger.LogError($"User registration failed: {evt.ErrorMessage}");
///         return LSPhaseResult.CONTINUE;
///     })
/// );
/// 
/// // Complex validation pipeline
/// var orderEvent = new OrderProcessedEvent(order, 5.99m, PaymentMethod.CreditCard, shippingAddress);
/// orderEvent.ProcessWith<OrderProcessedEvent>(dispatcher, builder => builder
///     .ValidateData<decimal>("processing.fee", fee => fee >= 0, "Fee cannot be negative")
///     .ValidateData<PaymentMethod>("payment.method", pm => pm != PaymentMethod.None, "Payment method required")
///     .OnValidation((evt, ctx) => {
///         if (evt.Instance.Total <= 0) {
///             evt.SetErrorMessage("Order total must be positive");
///             return LSPhaseResult.CANCEL;
///         }
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnPrepare((evt, ctx) => {
///         // Reserve inventory
///         foreach (var item in evt.Instance.Items) {
///             inventoryService.Reserve(item.ProductId, item.Quantity);
///         }
///         evt.SetData("inventory.reserved", true);
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnExecution((evt, ctx) => {
///         // Process payment
///         var paymentResult = paymentService.ProcessPayment(evt.Instance.Total, evt.PaymentMethod);
///         if (!paymentResult.Success) {
///             evt.SetErrorMessage($"Payment failed: {paymentResult.ErrorMessage}");
///             return LSPhaseResult.CANCEL;
///         }
///         evt.SetData("payment.transaction.id", paymentResult.TransactionId);
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnFinalize((evt, ctx) => {
///         // Commit inventory changes
///         inventoryService.CommitReservations();
///         evt.SetData("inventory.committed", true);
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnComplete((evt, ctx) => {
///         // Always log processing attempt
///         auditService.LogOrderProcessing(evt.Instance.Id, evt.IsCompleted, evt.ErrorMessage);
///         return LSPhaseResult.CONTINUE;
///     })
/// );
/// 
/// // Manual lifecycle management
/// var builder = myEvent.RegisterCallback<MyEvent>(dispatcher);
/// builder.OnValidation(ValidateHandler)
///        .OnExecution(ExecuteHandler);
/// var success = builder.ProcessAndCleanup();
/// ```
/// 
/// ### Conditional and Data-Driven Processing
/// ```csharp
/// paymentEvent.ProcessWith<PaymentEvent>(dispatcher, builder => builder
///     // Conditional processing based on amount
///     .If(evt => evt.Amount > 1000, premiumBuilder => premiumBuilder
///         .OnValidation((evt, ctx) => {
///             // Additional validation for large payments
///             if (!fraudDetectionService.ValidateHighValuePayment(evt)) {
///                 evt.SetErrorMessage("Payment flagged by fraud detection");
///                 return LSPhaseResult.CANCEL;
///             }
///             return LSPhaseResult.CONTINUE;
///         })
///         .OnExecution((evt, ctx) => {
///             // Special processing for high-value payments
///             premiumPaymentService.ProcessPayment(evt);
///             return LSPhaseResult.CONTINUE;
///         })
///     )
///     // Data-based conditional execution
///     .OnDataEquals("payment.type", "credit", (evt, ctx) => {
///         creditCardService.ProcessCreditPayment(evt);
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnDataEquals("payment.type", "debit", (evt, ctx) => {
///         debitCardService.ProcessDebitPayment(evt);
///         return LSPhaseResult.CONTINUE;
///     })
///     // Timeout handling
///     .OnTimeout(TimeSpan.FromSeconds(30), (evt, ctx) => {
///         evt.SetErrorMessage("Payment processing timed out");
///         return LSPhaseResult.CANCEL;
///     })
///     // Performance monitoring
///     .OnSlowProcessing(TimeSpan.FromSeconds(5), (evt, ctx) => {
///         performanceLogger.LogSlowPayment(evt.Amount, ctx.ElapsedTime);
///         return LSPhaseResult.CONTINUE;
///     })
///     .MeasureExecutionTime((evt, totalTime) => {
///         metricsService.RecordPaymentProcessingTime(evt.Amount, totalTime);
///     })
/// );
/// ```
/// 
/// ### Advanced Error Handling and Retry Logic
/// ```csharp
/// dataProcessingEvent.ProcessWith<DataProcessingEvent>(dispatcher, builder => builder
///     .OnValidation((evt, ctx) => {
///         if (!dataValidator.IsValid(evt.Instance.Data)) {
///             evt.SetErrorMessage("Invalid data format");
///             return LSPhaseResult.CANCEL;
///         }
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnExecution((evt, ctx) => {
///         try {
///             dataProcessor.Process(evt.Instance.Data);
///             evt.SetData("processing.completed", true);
///             return LSPhaseResult.CONTINUE;
///         } catch (TemporaryServiceException ex) {
///             evt.SetErrorMessage($"Temporary service error: {ex.Message}");
///             evt.SetData("retry.reason", ex.Message);
///             return LSPhaseResult.RETRY; // Will retry up to 3 times
///         } catch (PermanentException ex) {
///             evt.SetErrorMessage($"Permanent error: {ex.Message}");
///             return LSPhaseResult.CANCEL;
///         }
///     })
///     .RetryOnError(3) // Configure retry behavior
///     .OnError((evt, ctx) => {
///         // Handle final failure after retries
///         errorService.RecordProcessingFailure(evt.Instance.Data, evt.ErrorMessage);
///         notificationService.NotifyAdministrators(evt.ErrorMessage);
///         return LSPhaseResult.CONTINUE;
///     })
///     .OnSuccess((evt, ctx) => {
///         // Handle successful completion
///         auditService.RecordSuccessfulProcessing(evt.Instance.Data);
///         return LSPhaseResult.CONTINUE;
///     })
/// );
/// ```
/// 
/// ### Performance Monitoring
/// ```csharp
/// orderEvent.ProcessWith<OrderEvent>(dispatcher, builder => builder
///     .OnExecution((evt, ctx) => ProcessOrder(evt))
///     .OnSlowProcessing(TimeSpan.FromSeconds(5), (evt, ctx) => LogSlowOrder(evt))
///     .MeasureExecutionTime((evt, time) => RecordMetrics(evt, time))
/// );
/// ```
/// 
/// ### Parallel Processing and Composition
/// ```csharp
/// complexEvent.ProcessWith<ComplexBusinessEvent>(dispatcher, builder => builder
///     .OnValidation((evt, ctx) => {
///         // Primary validation
///         if (!businessRuleValidator.Validate(evt.Instance)) {
///             return LSPhaseResult.CANCEL;
///         }
///         return LSPhaseResult.CONTINUE;
///     })
///     // Parallel validation checks
///     .InParallel(parallelBuilder => parallelBuilder
///         .OnValidation((evt, ctx) => {
///             // Security validation
///             securityService.ValidatePermissions(evt.Instance);
///             return LSPhaseResult.CONTINUE;
///         })
///         .OnValidation((evt, ctx) => {
///             // Data integrity validation
///             dataIntegrityService.ValidateData(evt.Instance);
///             return LSPhaseResult.CONTINUE;
///         })
///     )
///     .Then(nextBuilder => nextBuilder
///         .OnExecution((evt, ctx) => {
///             // Execute after all validations pass
///             businessLogicService.Execute(evt.Instance);
///             return LSPhaseResult.CONTINUE;
///         })
///     )
/// );
/// ```
/// 
/// ### LSDispatcher Type and Methods
/// ```csharp
/// public class LSDispatcher {
///     // Singleton access
///     public static LSDispatcher Singleton { get; }
///     
///     // Fluent registration
///     public LSEventRegistration<TEvent> For<TEvent>() where TEvent : ILSEvent;
///     
///     // Simple registration
///     public Guid RegisterHandler<TEvent>(
///         LSPhaseHandler<TEvent> handler,
///         LSEventPhase phase = LSEventPhase.EXECUTE,
///         LSPhasePriority priority = LSPhasePriority.NORMAL
///     ) where TEvent : ILSEvent;
///     
///     // Full registration (internal)
///     internal Guid RegisterHandler<TEvent>(
///         LSPhaseHandler<TEvent> handler,
///         LSEventPhase phase,
///         LSPhasePriority priority,
///         Type? instanceType,
///         object? instance,
///         int maxExecutions,
///         Func<TEvent, bool>? condition
///     ) where TEvent : ILSEvent;
///     
///     // Event processing
///     public bool ProcessEvent<TEvent>(TEvent @event) where TEvent : ILSEvent;
///     
///     // Handler management
///     public bool UnregisterHandler(Guid handlerId);
///     
///     // Batch registration (internal optimization)
///     internal Guid RegisterBatchedHandlers<TEvent>(LSEventCallbackBatch<TEvent> batch) where TEvent : ILSEvent;
///     
///     // Monitoring and diagnostics
///     public int GetHandlerCount<TEvent>() where TEvent : ILSEvent;
///     public int GetTotalHandlerCount();
/// }
/// ```
/// 
/// ## High-Performance Event Processing (Batching Optimization)
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
/// is dramatically improved, especially for events with many handlers.
/// 
/// ## Comprehensive Type Information
/// 
/// ### Complete Event Processing Pipeline
/// ```csharp
/// // 1. Create event
/// var userEvent = new UserRegistrationEvent(user, "mobile_app");
/// 
/// // 2. Set up processing with full type safety
/// var success = userEvent.ProcessWith<UserRegistrationEvent>(dispatcher, builder => builder
///     .OnValidation((UserRegistrationEvent evt, LSPhaseContext ctx) => {
///         // Full type information available
///         User user = evt.Instance;                    // TInstance type
///         string source = evt.RegistrationSource;     // Event property
///         DateTime time = evt.RegistrationTime;       // Event property
///         Guid eventId = evt.Id;                       // Base event property
///         LSEventPhase phase = ctx.CurrentPhase;      // Context information
///         TimeSpan elapsed = ctx.ElapsedTime;         // Performance data
///         
///         // Store typed data
///         evt.SetData("validation.timestamp", DateTime.UtcNow);
///         evt.SetData("validation.source", source);
///         
///         return LSPhaseResult.CONTINUE;
///     })
/// );
/// 
/// // 3. Check results with full state information
/// if (success) {
///     Console.WriteLine($"Event {userEvent.Id} completed successfully");
///     Console.WriteLine($"Completed phases: {userEvent.CompletedPhases}");
///     
///     // Access typed data
///     if (userEvent.TryGetData<DateTime>("validation.timestamp", out var timestamp)) {
///         Console.WriteLine($"Validated at: {timestamp}");
///     }
/// } else {
///     Console.WriteLine($"Event failed: {userEvent.ErrorMessage}");
///     Console.WriteLine($"Current phase: {userEvent.CurrentPhase}");
///     Console.WriteLine($"Cancelled: {userEvent.IsCancelled}");
/// }
/// ```
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
