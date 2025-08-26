# LSUtils Event System - Comprehensive Documentation

A high-performance, phase-based event processing system with support for asynchronous operations, optimized handler batching, and both global and event-scoped processing patterns.

## Table of Contents

- [Core Event Types](#core-event-types)
- [Event Processing Framework](#event-processing-framework)
- [Handler and Context Types](#handler-and-context-types)
- [Asynchronous Processing with WAITING State](#asynchronous-processing-with-waiting-state)
- [Registration and Processing Types](#registration-and-processing-types)
- [Event Processing Engine](#event-processing-engine)
- [Performance Optimization](#performance-optimization-automatic-handler-batching)
- [Usage Guide](#complete-usage-guide)
- [Architecture Overview](#architecture-overview)
- [Best Practices](#best-practices)

## Core Event Types

### ILSEvent Interface (Public API)

```csharp
public interface ILSEvent {
    Guid Id { get; }                                        // Unique event identifier
    Type EventType { get; }                                 // Runtime type of the event
    DateTime CreatedAt { get; }                             // Event creation timestamp
    bool IsCancelled { get; }                               // Whether event was cancelled
    bool IsCompleted { get; }                               // Whether event processing completed
    bool IsWaiting { get; }                                 // Whether event is in async wait state
    LSEventPhase CurrentPhase { get; }                      // Current processing phase
    LSEventPhase CompletedPhases { get; }                   // Bitmask of completed phases
    string? ErrorMessage { get; }                           // Error message if failed
    IReadOnlyDictionary<string, object> Data { get; }       // Immutable view of event data
    T GetData<T>(string key);                               // Get typed data (throws if not found)
    bool TryGetData<T>(string key, out T value);            // Safe typed data access
    void ContinueProcessing();                              // Resume from WAITING state
}
```

### ILSMutableEvent Interface (Internal)

```csharp
internal interface ILSMutableEvent : ILSEvent {
    new bool IsCancelled { get; set; }
    new bool IsCompleted { get; set; }
    new bool IsWaiting { get; set; }
    new LSEventPhase CurrentPhase { get; set; }
    new LSEventPhase CompletedPhases { get; set; }
    new string? ErrorMessage { get; set; }
    void SetData(string key, object value);                 // Internal data modification
    void SetErrorMessage(string message);                   // Internal error setting
}
```

### LSBaseEvent Abstract Class

```csharp
public abstract class LSBaseEvent : ILSMutableEvent {
    // Auto-generated properties
    public Guid Id { get; }                                 // Unique identifier (auto-generated)
    public Type EventType { get; }                          // Runtime type (auto-set)
    public DateTime CreatedAt { get; }                      // Creation time (auto-set)
    
    // Processing state
    public bool IsCancelled { get; set; }                   // Cancellation state
    public bool IsCompleted { get; set; }                   // Completion state
    public bool IsWaiting { get; set; }                     // Async wait state
    public LSEventPhase CurrentPhase { get; set; }          // Current processing phase
    public LSEventPhase CompletedPhases { get; set; }       // Bitmask of completed phases
    public string? ErrorMessage { get; set; }               // Error message storage
    
    // Data management
    public IReadOnlyDictionary<string, object> Data { get; }    // Immutable data view
    public void SetData(string key, object value);              // Store typed data
    public T GetData<T>(string key);                            // Retrieve typed data
    public bool TryGetData<T>(string key, out T value);         // Safe data retrieval
    public void SetErrorMessage(string message);                // Set error state
    
    // Async processing control
    public void ContinueProcessing();                           // Resume from WAITING state
    
    // Event processing integration
    public LSEventCallbackBuilder<TEventType> RegisterCallback<TEventType>(LSDispatcher dispatcher);
    public bool ProcessWith<TEventType>(LSDispatcher dispatcher, Action<LSEventCallbackBuilder<TEventType>>? configure = null);
    public bool Process(LSDispatcher dispatcher);               // Simple processing
}
```

### LSEvent\<TInstance\> Generic Class

```csharp
public abstract class LSEvent<TInstance> : LSBaseEvent where TInstance : class {
    public TInstance Instance { get; }                      // Strongly-typed event source
    protected LSEvent(TInstance instance);                  // Constructor requires instance
}
```

## Event Processing Framework

### LSEventPhase Enum (Processing Phases)

```csharp
[Flags]
public enum LSEventPhase {
    VALIDATE = 1,        // Input validation, permission checks, early validation
    PREPARE = 2,         // Setup, resource allocation, state preparation
    EXECUTE = 4,         // Core business logic and main processing
    SUCCESS = 8,        // Success-only operations, side effects, result processing
    CANCEL = 16,         // Cancellation cleanup (pseudo-phase, only runs when cancelled)
    COMPLETE = 32        // Always runs regardless of success/failure (logging, metrics)
}
```

### LSPhasePriority Enum (Execution Priority)

```csharp
public enum LSPhasePriority {
    CRITICAL = 0,        // System-critical operations (highest priority)
    HIGH = 1,            // Important business logic
    NORMAL = 2,          // Standard operations (default)
    LOW = 3,             // Nice-to-have features
    BACKGROUND = 4       // Background operations (lowest priority)
}
```

### LSPhaseResult Enum (Handler Control Flow)

```csharp
public enum LSPhaseResult {
    CONTINUE,            // Continue to next handler in sequence
    SKIP_REMAINING,      // Skip remaining handlers in current phase
    CANCEL,              // Cancel entire event processing
    RETRY,               // Retry current handler (up to retry limit)
    WAITING              // Pause processing for async operation
}
```

## Handler and Context Types

### LSPhaseHandler Delegate

```csharp
public delegate LSPhaseResult LSPhaseHandler<in TEvent>(TEvent evt, LSPhaseContext context) 
    where TEvent : ILSEvent;
```

### LSPhaseContext Class (Execution Context)

```csharp
public class LSPhaseContext {
    public LSEventPhase CurrentPhase { get; }               // Current processing phase
    public DateTime StartTime { get; }                      // Phase execution start time
    public TimeSpan ElapsedTime { get; }                    // Time elapsed in current phase
    public int HandlerCount { get; }                        // Total handlers in current phase
    public bool HasErrors { get; }                          // Whether any errors occurred
    public IReadOnlyList<string> Errors { get; }            // Collection of error messages
}
```

### LSHandlerRegistration Class (Internal)

```csharp
internal class LSHandlerRegistration {
    public Guid Id { get; set; }                            // Unique handler identifier
    public Type EventType { get; set; }                     // Target event type
    public Func<ILSEvent, LSPhaseContext, LSPhaseResult> Handler { get; set; }  // Handler function
    public LSEventPhase Phase { get; set; }                 // Target execution phase
    public LSPhasePriority Priority { get; set; }           // Execution priority
    public Type? InstanceType { get; set; }                 // Optional instance type filter
    public object? Instance { get; set; }                   // Optional specific instance filter
    public int MaxExecutions { get; set; }                  // Maximum execution count
    public Func<ILSEvent, bool>? Condition { get; set; }    // Optional execution condition
    public int ExecutionCount { get; set; }                 // Current execution count
}
```

## Enhanced Cancel Handling with CANCEL Phase

The event system provides comprehensive cancellation support through a dedicated CANCEL pseudo-phase that runs only when events are cancelled, enabling sophisticated cleanup and rollback operations.

### CANCEL Phase Overview

The **CANCEL phase** is a special pseudo-phase that:

- **Only executes when an event is cancelled** (when any handler returns `LSPhaseResult.CANCEL`)
- **Runs instead of SUCCESS phase, before COMPLETE** in the processing pipeline
- **Supports all standard features**: priority ordering, conditional execution, async operations
- **Uses the same execution model** as other phases for consistency

### Processing Flow with CANCEL Phase

```text
Normal Flow:    VALIDATE → PREPARE → EXECUTE → SUCCESS → COMPLETE
Cancelled Flow: [VALIDATE|PREPARE|EXECUTE] → CANCEL → COMPLETE
                    ↑                            ↑
              (cancellation)              (cleanup handlers)
```

### OnCancel Method Overloads

The `LSEventCallbackBuilder` provides multiple OnCancel overloads for different use cases:

#### 1. Basic Cancel Handler

```csharp
.OnCancel((evt, ctx) => {
    // Full handler with access to context
    Console.WriteLine($"Event cancelled in phase: {ctx.CurrentPhase}");
    return LSPhaseResult.CONTINUE;
})
```

#### 2. Simple Action (Convenience Method)

```csharp
.OnCancel(evt => {
    // Simple cleanup action without context
    evt.SetData("cleanup.performed", true);
})
```

#### 3. Priority-Based Execution

```csharp
.OnCancel(criticalCleanupHandler, LSPhasePriority.CRITICAL)  // Runs first
.OnCancel(normalCleanupHandler, LSPhasePriority.NORMAL)      // Runs second
.OnCancel(backgroundLogHandler, LSPhasePriority.LOW)         // Runs last
```

#### 4. Conditional Execution

```csharp
.OnCancelWhen(databaseRollbackHandler, evt => evt.GetData<bool>("database.transaction.started"))
.OnCancelWhen(fileCleanupHandler, evt => evt.Data.ContainsKey("temp.files.created"))
```

#### 5. Priority + Conditional (Full Control)

```csharp
.OnCancelWhen(
    criticalSecurityCleanup, 
    LSPhasePriority.CRITICAL, 
    evt => evt.GetData<string>("security.level") == "HIGH"
)
```

### Cancel Handler Features

- **Priority Ordering**: CRITICAL → HIGH → NORMAL → LOW → BACKGROUND
- **Conditional Execution**: Handlers only run when conditions are met
- **Async Support**: Can return `LSPhaseResult.WAITING` for async cleanup operations
- **Error Handling**: Exceptions in cancel handlers don't stop other cancel handlers
- **Data Access**: Full access to event data and context information

### Best Practices for Cancel Handling

#### ✅ Use appropriate priorities

```csharp
.OnCancel(securityCleanup, LSPhasePriority.CRITICAL)     // Security first
.OnCancel(businessLogicRollback, LSPhasePriority.HIGH)   // Business logic second
.OnCancel(auditLogging, LSPhasePriority.LOW)             // Logging last
```

#### ✅ Handle async cleanup properly

```csharp
.OnCancel((evt, ctx) => {
    asyncCleanupService.StartCleanup(evt.Instance, () => {
        evt.SetData("async.cleanup.completed", true);
        evt.ContinueProcessing();
    });
    return LSPhaseResult.WAITING;
})
```

#### ✅ Use conditions for specific cleanup

```csharp
.OnCancelWhen(
    databaseRollback, 
    evt => evt.TryGetData<string>("transaction.id", out _)
)
```

## Asynchronous Processing with WAITING State

The event system supports sophisticated asynchronous operations through the `WAITING` state, enabling handlers to pause event processing for external operations and resume when ready.

### Core WAITING State Features

- **Pause/Resume Control**: Handlers can return `LSPhaseResult.WAITING` to pause processing
- **Event-Controlled Resumption**: Only the event itself can resume via `ContinueProcessing()`
- **State Preservation**: Event maintains exact phase and handler position during wait
- **Automatic Metadata**: System tracks waiting start time, phase, and handler information
- **Thread-Safe Resumption**: `ContinueProcessing()` can be called from any thread
- **Deadlock Potential**: Events can deadlock if `ContinueProcessing()` is never called

### WAITING State Properties and Methods

```csharp
public interface ILSEvent {
    bool IsWaiting { get; }                                 // Current waiting status
    void ContinueProcessing();                              // Resume from waiting state
}

// Auto-generated metadata when entering WAITING state:
// "waiting.phase" (string)          - Phase where waiting started
// "waiting.handler.id" (string)     - ID of handler that initiated waiting
// "waiting.started.at" (DateTime)   - Timestamp when waiting began
// "waiting.handler.count" (int)     - Number of handlers executed in phase
```

### Async Processing Pattern

```csharp
public LSPhaseResult HandleAsyncOperation(MyEvent evt, LSPhaseContext ctx) {
    // Start asynchronous operation (database, network, etc.)
    asyncService.StartOperation(evt.Instance, (result) => {
        // Store async result in event data
        evt.SetData("async.result", result);
        evt.SetData("async.completed.at", DateTime.UtcNow);
        
        // Resume event processing from exact point where it paused
        evt.ContinueProcessing();
    });
    
    // Pause event processing until async operation completes
    return LSPhaseResult.WAITING;
}
```

### Multiple WAITING Cycles

```csharp
public LSPhaseResult HandleMultiStepAsync(MyEvent evt, LSPhaseContext ctx) {
    // Check if first async step is complete
    if (!evt.TryGetData<bool>("step1.complete", out _)) {
        StartAsyncStep1(evt, () => {
            evt.SetData("step1.complete", true);
            evt.ContinueProcessing();  // Resume for step 2
        });
        return LSPhaseResult.WAITING;
    }
    
    // Check if second async step is complete
    if (!evt.TryGetData<bool>("step2.complete", out _)) {
        StartAsyncStep2(evt, () => {
            evt.SetData("step2.complete", true);
            evt.ContinueProcessing();  // Resume to continue processing
        });
        return LSPhaseResult.WAITING;
    }
    
    // Both steps complete, continue normally
    return LSPhaseResult.CONTINUE;
}
```

### Important WAITING State Considerations

- **Deadlock Prevention**: Always ensure `ContinueProcessing()` is called in all code paths
- **Error Handling**: Handle async operation failures and call `ContinueProcessing()` even on errors
- **State Validation**: System validates event is actually waiting before allowing resumption
- **Single Resume Per Wait**: Each `WAITING` return requires exactly one `ContinueProcessing()` call
- **Thread Safety**: Resumption is thread-safe but event data modification may need synchronization
- **Dispatcher Dependency**: Events must use `Process()` or `ProcessWith()` for resumption to work

## Registration and Processing Types

### LSEventRegistration\<TEvent\> Class (Global Handler Registration)

```csharp
public class LSEventRegistration<TEvent> where TEvent : ILSEvent {
    public LSEventRegistration<TEvent> InPhase(LSEventPhase phase);          // Set target phase
    public LSEventRegistration<TEvent> WithPriority(LSPhasePriority priority); // Set execution priority
    public LSEventRegistration<TEvent> ForInstance<TInstance>(TInstance instance) where TInstance : class; // Instance filter
    public LSEventRegistration<TEvent> MaxExecutions(int maxExecutions);     // Limit executions
    public LSEventRegistration<TEvent> When(Func<TEvent, bool> condition);   // Conditional execution
    public Guid Register(LSPhaseHandler<TEvent> handler);                    // Register handler
}
```

### LSEventCallbackBuilder\<TEvent\> Class (Event-Scoped Handlers - 30+ Methods)

The callback builder provides over 30 specialized methods organized into categories:

#### Core Phase Handlers (5 methods)

```csharp
public LSEventCallbackBuilder<TEvent> OnValidation(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnPrepare(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnExecution(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnSUCCESS(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnComplete(LSPhaseHandler<TEvent> handler);
```

#### Priority Variants (15 methods)

```csharp
public LSEventCallbackBuilder<TEvent> OnCriticalValidation(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnHighValidation(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnLowValidation(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnBackgroundValidation(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnCriticalPrepare(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnHighPrepare(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnLowPrepare(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnBackgroundPrepare(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnCriticalExecution(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnHighPriorityExecution(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnLowExecution(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnBackgroundExecution(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnCriticalSuccess(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnHighSuccess(LSPhaseHandler<TEvent> handler);
public LSEventCallbackBuilder<TEvent> OnLowSuccess(LSPhaseHandler<TEvent> handler);
```

#### Conditional Handlers (12 methods)

```csharp
public LSEventCallbackBuilder<TEvent> OnSuccess(LSPhaseHandler<TEvent> handler);         // When no errors
public LSEventCallbackBuilder<TEvent> OnError(LSPhaseHandler<TEvent> handler);           // When errors occur

// Enhanced Cancel Handling (5 overloads)
public LSEventCallbackBuilder<TEvent> OnCancel(LSPhaseHandler<TEvent> handler);          // Basic cancel handler
public LSEventCallbackBuilder<TEvent> OnCancel(Action<TEvent> action);                   // Simple cancel action
public LSEventCallbackBuilder<TEvent> OnCancel(LSPhaseHandler<TEvent> handler, LSPhasePriority priority); // Priority cancel
public LSEventCallbackBuilder<TEvent> OnCancelWhen(LSPhaseHandler<TEvent> handler, Func<TEvent, bool> condition); // Conditional cancel
public LSEventCallbackBuilder<TEvent> OnCancelWhen(LSPhaseHandler<TEvent> handler, LSPhasePriority priority, Func<TEvent, bool> condition); // Priority + conditional

public LSEventCallbackBuilder<TEvent> OnDataPresent(string key, LSPhaseHandler<TEvent> handler); // When data exists
public LSEventCallbackBuilder<TEvent> OnDataEquals<T>(string key, T value, LSPhaseHandler<TEvent> handler); // When data matches
public LSEventCallbackBuilder<TEvent> OnTimeout(TimeSpan timeout, LSPhaseHandler<TEvent> handler); // On timeout
public LSEventCallbackBuilder<TEvent> OnSlowProcessing(TimeSpan threshold, LSPhaseHandler<TEvent> handler); // On slow execution
```

#### Action Shortcuts (4 methods)

```csharp
public LSEventCallbackBuilder<TEvent> DoOnValidation(Action<TEvent> action);             // Simple validation action
public LSEventCallbackBuilder<TEvent> DoOnExecution(Action<TEvent> action);              // Simple execution action
public LSEventCallbackBuilder<TEvent> DoOnComplete(Action<TEvent> action);               // Simple completion action
public LSEventCallbackBuilder<TEvent> DoWhen(Func<TEvent, bool> condition, Action<TEvent> action); // Conditional action
```

#### Data Manipulation (4 methods)

```csharp
public LSEventCallbackBuilder<TEvent> SetData(string key, object value);                 // Set data value
public LSEventCallbackBuilder<TEvent> SetDataWhen(Func<TEvent, bool> condition, string key, object value); // Conditional data
public LSEventCallbackBuilder<TEvent> TransformData<T>(string key, Func<T, T> transform); // Transform existing data
public LSEventCallbackBuilder<TEvent> ValidateData<T>(string key, Func<T, bool> validator, string errorMessage); // Data validation
```

#### Logging and Monitoring (3 methods)

```csharp
public LSEventCallbackBuilder<TEvent> LogPhase(LSEventPhase phase, Action<TEvent, LSPhaseContext> logger); // Phase logging
public LSEventCallbackBuilder<TEvent> LogOnError(Action<TEvent, string> logger);         // Error logging
public LSEventCallbackBuilder<TEvent> MeasureExecutionTime(Action<TEvent, TimeSpan> onComplete); // Performance measurement
```

#### Error Handling (2 methods)

```csharp
public LSEventCallbackBuilder<TEvent> CancelIf(Func<TEvent, bool> condition);            // Conditional cancellation
public LSEventCallbackBuilder<TEvent> RetryOnError(int maxRetries);                      // Retry configuration
```

#### Composition and Control Flow (3 methods)

```csharp
public LSEventCallbackBuilder<TEvent> Then(Action<LSEventCallbackBuilder<TEvent>> configure); // Sequential composition
public LSEventCallbackBuilder<TEvent> If(Func<TEvent, bool> condition, Action<LSEventCallbackBuilder<TEvent>> configure); // Conditional branching
public LSEventCallbackBuilder<TEvent> InParallel(Action<LSEventCallbackBuilder<TEvent>> configure); // Parallel execution
```

#### Processing Control (1 method)

```csharp
public bool ProcessAndCleanup();                                          // Execute and cleanup
```

### LSEventCallbackBatch\<TEvent\> Class (Internal Optimization)

```csharp
internal class LSEventCallbackBatch<TEvent> where TEvent : ILSEvent {
    public TEvent TargetEvent { get; }                                       // Event being processed
    public Dictionary<LSEventPhase, List<BatchedHandler>> HandlersByPhase { get; } // Phase-organized handlers
    public IEnumerable<BatchedHandler> GetHandlersForPhase(LSEventPhase phase); // Phase-specific retrieval
    public void AddHandler(LSEventPhase phase, LSPhasePriority priority, LSPhaseHandler<TEvent> handler, Func<TEvent, bool>? condition = null);
}

internal class BatchedHandler {
    public LSPhasePriority Priority { get; set; }                            // Handler priority
    public LSPhaseHandler<TEvent> Handler { get; set; }                      // Handler function
    public Func<TEvent, bool>? Condition { get; set; }                       // Optional condition
}
```

## Event Processing Engine

### LSDispatcher Class (Central Processing Engine)

```csharp
public class LSDispatcher {
    // === SINGLETON ACCESS ===
    public static LSDispatcher Singleton { get; }                           // Global instance
    
    // === FLUENT REGISTRATION API ===
    public LSEventRegistration<TEvent> For<TEvent>() where TEvent : ILSEvent; // Start registration chain
    
    // === SIMPLE REGISTRATION ===
    public Guid RegisterHandler<TEvent>(                                     // Quick handler registration
        LSPhaseHandler<TEvent> handler,
        LSEventPhase phase = LSEventPhase.EXECUTE,
        LSPhasePriority priority = LSPhasePriority.NORMAL
    ) where TEvent : ILSEvent;
    
    // === FULL REGISTRATION (Internal) ===
    internal Guid RegisterHandler<TEvent>(                                   // Complete registration control
        LSPhaseHandler<TEvent> handler,
        LSEventPhase phase,
        LSPhasePriority priority,
        Type? instanceType,
        object? instance,
        int maxExecutions,
        Func<TEvent, bool>? condition
    ) where TEvent : ILSEvent;
    
    // === EVENT PROCESSING ===
    public bool ProcessEvent<TEvent>(TEvent @event) where TEvent : ILSEvent; // Process single event
    public bool ContinueProcessing<TEvent>(TEvent @event) where TEvent : ILSEvent; // Resume from WAITING
    
    // === HANDLER MANAGEMENT ===
    public bool UnregisterHandler(Guid handlerId);                          // Remove handler
    
    // === OPTIMIZATION FEATURES (Internal) ===
    internal Guid RegisterBatchedHandlers<TEvent>(LSEventCallbackBatch<TEvent> batch) where TEvent : ILSEvent; // Batch optimization
    internal LSPhaseResult ExecuteBatchedHandlers<TEvent>(                  // Execute batch
        TEvent evt, 
        LSEventCallbackBatch<TEvent> batch, 
        LSEventPhase phase, 
        LSPhaseContext ctx
    ) where TEvent : ILSEvent;
    
    // === DIAGNOSTICS AND MONITORING ===
    public int GetHandlerCount<TEvent>() where TEvent : ILSEvent;           // Handlers for event type
    public int GetTotalHandlerCount();                                       // Total registered handlers
}
```

## Performance Optimization: Automatic Handler Batching

The LSEventCallbackBuilder uses advanced internal optimization to dramatically improve performance by collecting multiple handler registrations into a single batch operation.

### Optimization Benefits

- **Reduced Registration Overhead**: O(N) → O(1) registration complexity per event
- **Memory Efficiency**: Single composite handler instead of N individual handlers
- **Automatic Cleanup**: One-time execution with automatic cleanup (MaxExecutions = 1)
- **Phase-Aware Execution**: Only relevant handlers executed per phase
- **Transparent Operation**: No API changes required - existing code benefits immediately

### Example: 8 Handlers → 1 Registration

```csharp
// This registers 8 individual handlers but optimizes to 1 internal registration
complexEvent.ProcessWith<ComplexBusinessEvent>(dispatcher, builder => builder
    .OnCriticalValidation((evt, ctx) => ValidateBusinessRules(evt))      // Handler 1
    .OnValidation((evt, ctx) => ValidateInputs(evt))                      // Handler 2
    .OnPrepare((evt, ctx) => SetupResources(evt))                         // Handler 3
    .OnHighPriorityExecution((evt, ctx) => ExecuteCore(evt))             // Handler 4
    .OnExecution((evt, ctx) => ExecuteSecondary(evt))                     // Handler 5
    .OnSUCCESS((evt, ctx) => CleanupResources(evt))                      // Handler 6
    .OnSuccess((evt, ctx) => LogSuccess(evt))                             // Handler 7
    .OnError((evt, ctx) => HandleErrors(evt))                             // Handler 8
);

// Internal optimization result:
// - 1 composite handler registration (not 8)
// - Automatic phase-aware execution
// - Automatic cleanup after event completes
// - ~8x reduction in registration overhead
```

### Performance Impact

```csharp
// WITHOUT optimization (theoretical):
for (int i = 0; i < 8; i++) {
    dispatcher.RegisterHandler(handler[i], phase[i], priority[i]);  // 8 registrations
}
// + 8 cleanup operations after event

// WITH optimization (actual):
dispatcher.RegisterBatchedHandlers(batchOf8Handlers);              // 1 registration
// + automatic cleanup via MaxExecutions=1
```

## Complete Usage Guide

### Basic Event-Scoped Processing

```csharp
// Simple event processing with automatic cleanup
var userEvent = new UserRegistrationEvent(user, "mobile_app");
var success = userEvent.ProcessWith<UserRegistrationEvent>(dispatcher, builder => builder
    .OnValidation((evt, ctx) => {
        if (string.IsNullOrEmpty(evt.Instance.Email)) {
            evt.SetErrorMessage("Email is required");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    })
    .OnExecution((evt, ctx) => {
        userService.CreateUser(evt.Instance);
        evt.SetData("user.created", true);
        return LSPhaseResult.CONTINUE;
    })
    .OnSuccess((evt, ctx) => {
        emailService.SendWelcomeEmail(evt.Instance.Email);
        return LSPhaseResult.CONTINUE;
    })
    .OnError((evt, ctx) => {
        logger.LogError($"User registration failed: {evt.ErrorMessage}");
        return LSPhaseResult.CONTINUE;
    })
);

// Check results with full state information
if (success) {
    Console.WriteLine($"Event {userEvent.Id} completed successfully");
    Console.WriteLine($"Completed phases: {userEvent.CompletedPhases}");
    if (userEvent.TryGetData<bool>("user.created", out var created) && created) {
        Console.WriteLine("User successfully created");
    }
} else {
    Console.WriteLine($"Event failed: {userEvent.ErrorMessage}");
    Console.WriteLine($"Current phase: {userEvent.CurrentPhase}");
    Console.WriteLine($"Cancelled: {userEvent.IsCancelled}");
}
```

### Asynchronous Payment with WAITING State

```csharp
var paymentEvent = new PaymentProcessingEvent(payment);
var success = paymentEvent.ProcessWith<PaymentProcessingEvent>(dispatcher, builder => builder
    .OnValidation((evt, ctx) => {
        if (evt.Instance.Amount <= 0) {
            evt.SetErrorMessage("Invalid payment amount");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    })
    .OnExecution((evt, ctx) => {
        // Start async payment processing
        paymentGateway.ProcessPaymentAsync(evt.Instance, (result) => {
            // Store result and resume processing
            evt.SetData("payment.result", result);
            evt.SetData("payment.completed.at", DateTime.UtcNow);
            evt.ContinueProcessing();  // Resume from waiting state
        });
        
        // Pause event processing until payment completes
        return LSPhaseResult.WAITING;
    })
    .OnSUCCESS((evt, ctx) => {
        // This will execute after payment completes
        if (evt.TryGetData<PaymentResult>("payment.result", out var result)) {
            if (result.Success) {
                evt.SetData("payment.transaction.id", result.TransactionId);
                auditService.LogSuccessfulPayment(evt.Instance, result);
            } else {
                evt.SetErrorMessage($"Payment failed: {result.ErrorMessage}");
            }
        }
        return LSPhaseResult.CONTINUE;
    })
);
```

### Advanced Data Validation and Transformation

```csharp
var orderEvent = new OrderProcessingEvent(order);
orderEvent.ProcessWith<OrderProcessingEvent>(dispatcher, builder => builder
    // Automatic data validation with error handling
    .ValidateData<decimal>("order.total", total => total > 0, "Order total must be positive")
    .ValidateData<string>("customer.email", email => IsValidEmail(email), "Invalid email format")
    .ValidateData<List<OrderItem>>("order.items", items => items.Count > 0, "Order must have items")
    
    // Data transformation
    .TransformData<decimal>("order.total", total => Math.Round(total, 2))  // Round to 2 decimals
    .SetData("processing.started.at", DateTime.UtcNow)
    
    // Conditional processing based on data
    .OnDataEquals("order.type", "premium", (evt, ctx) => {
        premiumOrderService.ProcessPremiumOrder(evt.Instance);
        evt.SetData("premium.processed", true);
        return LSPhaseResult.CONTINUE;
    })
    .OnDataPresent("customer.vip.status", (evt, ctx) => {
        vipService.ApplyVipBenefits(evt.Instance);
        return LSPhaseResult.CONTINUE;
    })
);
```

### Performance Monitoring and Error Handling

```csharp
var complexEvent = new DataProcessingEvent(largeDataSet);
complexEvent.ProcessWith<DataProcessingEvent>(dispatcher, builder => builder
    .OnValidation((evt, ctx) => {
        if (!dataValidator.IsValid(evt.Instance.Data)) {
            evt.SetErrorMessage("Invalid data format");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    })
    .OnExecution((evt, ctx) => {
        try {
            dataProcessor.ProcessLargeDataSet(evt.Instance.Data);
            evt.SetData("processing.success", true);
            return LSPhaseResult.CONTINUE;
        } catch (TemporaryServiceException ex) {
            evt.SetErrorMessage($"Temporary error: {ex.Message}");
            return LSPhaseResult.RETRY;  // Will retry up to limit
        } catch (Exception ex) {
            evt.SetErrorMessage($"Processing failed: {ex.Message}");
            return LSPhaseResult.CANCEL;
        }
    })
    // Performance monitoring
    .OnSlowProcessing(TimeSpan.FromSeconds(5), (evt, ctx) => {
        logger.LogWarning($"Slow processing detected: {ctx.ElapsedTime}");
        performanceMetrics.RecordSlowEvent(evt.Instance, ctx.ElapsedTime);
        return LSPhaseResult.CONTINUE;
    })
    .MeasureExecutionTime((evt, totalTime) => {
        metricsService.RecordProcessingTime(evt.Instance.Size, totalTime);
    })
    // Timeout handling
    .OnTimeout(TimeSpan.FromMinutes(2), (evt, ctx) => {
        evt.SetErrorMessage("Processing timed out");
        cleanupService.CleanupPartialProcessing(evt.Instance);
        return LSPhaseResult.CANCEL;
    })
    // Retry configuration
    .RetryOnError(3)  // Retry failed operations up to 3 times
    // Error handling
    .OnError((evt, ctx) => {
        errorReportingService.ReportError(evt.Instance, evt.ErrorMessage);
        notificationService.NotifyAdministrators(evt.ErrorMessage);
        return LSPhaseResult.CONTINUE;
    })
);
```

### Global Handler Registration for Cross-Cutting Concerns

```csharp
// Global security validation for all user registration events
dispatcher.For<UserRegistrationEvent>()
    .InPhase(LSEventPhase.VALIDATE)
    .WithPriority(LSPhasePriority.CRITICAL)
    .Register((evt, ctx) => {
        if (!securityService.ValidateUserCreation(evt.Instance)) {
            evt.SetErrorMessage("Security validation failed");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    });

// Global audit logging for all events
dispatcher.For<ILSEvent>()
    .InPhase(LSEventPhase.COMPLETE)
    .WithPriority(LSPhasePriority.BACKGROUND)
    .Register((evt, ctx) => {
        auditService.LogEventCompletion(evt.Id, evt.EventType, evt.IsCompleted, evt.ErrorMessage);
        return LSPhaseResult.CONTINUE;
    });

// Instance-specific handler for premium users
dispatcher.For<UserRegistrationEvent>()
    .InPhase(LSEventPhase.EXECUTE)
    .ForInstance(premiumUser)
    .Register((evt, ctx) => {
        premiumService.SetupPremiumAccount(evt.Instance);
        evt.SetData("premium.setup.completed", true);
        return LSPhaseResult.CONTINUE;
    });
```

## Architecture Overview

### Processing Pipeline

```text
Event Creation → Phase-Based Processing → Automatic Cleanup
      ↓               ↓                        ↓
  LSBaseEvent    VALIDATE→PREPARE→         Batch handlers
  LSEvent<T>     EXECUTE→SUCCESS→         automatically
                 COMPLETE                  unregistered
```

### Core Components Interaction

```text
LSDispatcher (Central Engine)
    ├── LSEventRegistration (Global handlers)
    ├── LSEventCallbackBuilder (Event-scoped handlers) 
    │   └── LSEventCallbackBatch (Internal optimization)
    ├── LSPhaseContext (Execution metadata)
    └── LSHandlerRegistration (Internal state)
```

### Processing Flow

1. **Event Creation**: `new MyEvent(data)` → Auto-generated ID, timestamp, type
2. **Handler Registration**: Global handlers or event-scoped builders
3. **Phase Execution**: VALIDATE → PREPARE → EXECUTE → SUCCESS → COMPLETE
4. **Priority Ordering**: CRITICAL → HIGH → NORMAL → LOW → BACKGROUND within each phase
5. **Result Handling**: CONTINUE, SKIP_REMAINING, CANCEL, RETRY, WAITING
6. **Automatic Cleanup**: Event-scoped handlers auto-removed after processing

## Best Practices

### Event Design

#### ✅ Good: Clear, specific event types

```csharp
public class UserRegistrationEvent : LSEvent<User> {
    public string RegistrationSource { get; }
    public bool RequiresEmailVerification { get; }
    
    public UserRegistrationEvent(User user, string source, bool requiresVerification = true) 
        : base(user) {
        RegistrationSource = source;
        RequiresEmailVerification = requiresVerification;
        
        // Store structured data for handlers
        SetData("registration.source", source);
        SetData("email.verification.required", requiresVerification);
    }
}
```

#### ❌ Avoid: Generic, unclear events

```csharp
public class GenericEvent : LSBaseEvent {
    public object Data { get; set; }  // Too generic
}
```

### Handler Organization

#### ✅ Good: Use appropriate phases

```csharp
.OnValidation((evt, ctx) => ValidateInput(evt))      // Input validation
.OnPrepare((evt, ctx) => ReserveResources(evt))      // Resource allocation
.OnExecution((evt, ctx) => ProcessCore(evt))         // Main logic
.OnSUCCESS((evt, ctx) => CommitChanges(evt))        // Commit/cleanup
.OnComplete((evt, ctx) => LogCompletion(evt))        // Always runs
```

#### ✅ Good: Use priorities appropriately

```csharp
.OnCriticalValidation((evt, ctx) => SecurityCheck(evt))      // Critical first
.OnValidation((evt, ctx) => BusinessValidation(evt))         // Normal priority
.OnBackgroundValidation((evt, ctx) => LogValidation(evt))    // Background last
```

### Error Handling Strategy

#### ✅ Good: Comprehensive error handling

```csharp
.OnExecution((evt, ctx) => {
    try {
        ProcessData(evt.Instance);
        return LSPhaseResult.CONTINUE;
    } catch (ValidationException ex) {
        evt.SetErrorMessage($"Validation failed: {ex.Message}");
        return LSPhaseResult.CANCEL;  // Don't retry validation errors
    } catch (TemporaryServiceException ex) {
        evt.SetErrorMessage($"Service temporarily unavailable: {ex.Message}");
        return LSPhaseResult.RETRY;   // Retry temporary failures
    } catch (Exception ex) {
        evt.SetErrorMessage($"Unexpected error: {ex.Message}");
        logger.LogError(ex, "Unexpected error in event processing");
        return LSPhaseResult.CANCEL;  // Cancel on unexpected errors
    }
})
.OnError((evt, ctx) => {
    // Always handle errors gracefully
    errorReportingService.ReportError(evt);
    return LSPhaseResult.CONTINUE;  // Continue to complete phase
})
```

### Async Processing Best Practices

#### ✅ Good: Proper async handling with error handling

```csharp
.OnExecution((evt, ctx) => {
    try {
        asyncService.StartOperation(evt.Instance, (result, error) => {
            if (error != null) {
                evt.SetErrorMessage($"Async operation failed: {error.Message}");
                evt.SetData("async.error", error);
            } else {
                evt.SetData("async.result", result);
                evt.SetData("async.completed.at", DateTime.UtcNow);
            }
            
            // Always resume processing
            evt.ContinueProcessing();
        });
        
        return LSPhaseResult.WAITING;
    } catch (Exception ex) {
        evt.SetErrorMessage($"Failed to start async operation: {ex.Message}");
        return LSPhaseResult.CANCEL;  // Don't wait if we can't start
    }
})
```

#### ❌ Avoid: Async without error handling

```csharp
.OnExecution((evt, ctx) => {
    asyncService.StartOperation(evt.Instance, (result) => {
        evt.SetData("result", result);
        evt.ContinueProcessing();  // What if operation fails?
    });
    return LSPhaseResult.WAITING;
})
```

### Performance Optimization Guidelines

#### ✅ Good: Use event-scoped builders for complex processing

```csharp
myEvent.ProcessWith<MyEvent>(dispatcher, builder => builder
    .OnValidation(handler1)
    .OnValidation(handler2)      // All optimized into single registration
    .OnExecution(handler3)
    .OnSUCCESS(handler4)
);
```

#### ✅ Good: Use global handlers for cross-cutting concerns

```csharp
dispatcher.For<ILSEvent>()                    // All events
    .InPhase(LSEventPhase.COMPLETE)
    .WithPriority(LSPhasePriority.BACKGROUND)
    .Register(auditHandler);
```

#### ✅ Good: Use data validation methods for clean code

```csharp
.ValidateData<string>("email", email => IsValidEmail(email), "Invalid email")
.ValidateData<int>("age", age => age >= 18, "Must be 18 or older")
```

#### ❌ Avoid: Manual validation in every handler

```csharp
.OnValidation((evt, ctx) => {
    if (!evt.TryGetData<string>("email", out var email) || !IsValidEmail(email)) {
        evt.SetErrorMessage("Invalid email");
        return LSPhaseResult.CANCEL;
    }
    return LSPhaseResult.CONTINUE;
})
```

### Data Management Best Practices

#### ✅ Good: Use typed data access

```csharp
if (evt.TryGetData<UserProfile>("user.profile", out var profile)) {
    ProcessProfile(profile);
}
```

#### ✅ Good: Use structured data keys

```csharp
evt.SetData("payment.transaction.id", transactionId);
evt.SetData("payment.processed.at", DateTime.UtcNow);
evt.SetData("payment.amount", amount);
```

#### ❌ Avoid: Untyped or unclear data keys

```csharp
evt.SetData("data", someObject);  // Too generic
evt.SetData("x", value);          // Unclear meaning
```

## Key Architectural Benefits

### Thread Safety

- All components are thread-safe for concurrent access
- `ContinueProcessing()` can be called from any thread
- Event data modifications are internally synchronized

### Type Safety

- Compile-time type checking for all event types and handlers
- Generic constraints ensure proper event-handler matching
- Typed data access with automatic casting and validation

### Performance Optimization

- **Batching Optimization**: Multiple handlers → single registration
- **Phase-Aware Execution**: Only relevant handlers run per phase
- **Memory Efficiency**: Automatic cleanup prevents handler accumulation
- **Priority Ordering**: Efficient execution order within phases

### Flexibility

- **Multiple Registration Patterns**: Global and event-scoped approaches
- **Rich Conditional Logic**: Data-based, state-based, and custom conditions
- **Comprehensive Error Handling**: Built-in retry, cancellation, and error reporting
- **Asynchronous Support**: Full async operation support with pause/resume

### Monitoring and Debugging

- **Built-in Context**: Execution timing, error tracking, handler counts
- **Automatic Metadata**: Waiting state tracking, phase completion tracking
- **Performance Metrics**: Execution time measurement, slow operation detection
- **Error Reporting**: Structured error messages and state preservation
