# LSUtils Event System

A high-performance, phase-based event processing system with support for asynchronous operations, event-scoped handlers, and sophisticated cancellation handling.

## Table of Contents

- [Core Concepts](#core-concepts)
- [Event Types](#event-types)
- [Phase-Based Processing](#phase-based-processing)
- [Event-Scoped Handlers](#event-scoped-handlers)
- [Cancellation Handling](#cancellation-handling)
- [Asynchronous Processing](#asynchronous-processing)
- [Usage Examples](#usage-examples)

## Core Concepts

The LSEventSystem is built around **phase-based processing** where events flow through well-defined phases:

```text
VALIDATE → PREPARE → EXECUTE → SUCCESS → COMPLETE
                                  ↓
                              CANCEL → COMPLETE (when cancelled)
```

Each phase has a specific purpose:

- **VALIDATE**: Input validation, permission checks, early validation
- **PREPARE**: Setup, resource allocation, state preparation  
- **EXECUTE**: Core business logic and main processing
- **SUCCESS**: Success-only operations, result processing (only when not cancelled)
- **CANCEL**: Cancellation cleanup, rollback operations (only when cancelled)
- **COMPLETE**: Always runs regardless of success/failure (logging, metrics)

## Event Types

### ILSEvent Interface (Public API)

```csharp
public interface ILSEvent {
    Guid Id { get; }                                    // Unique event identifier
    Type EventType { get; }                             // Runtime type
    DateTime CreatedAt { get; }                         // Creation timestamp
    bool IsCancelled { get; }                           // Cancellation status
    bool IsCompleted { get; }                           // Completion status
    bool IsWaiting { get; }                             // Async wait status
    LSEventPhase CurrentPhase { get; }                  // Current phase
    string? ErrorMessage { get; }                       // Error message if failed
    IReadOnlyDictionary<string, object> Data { get; }   // Event data
    T GetData<T>(string key);                           // Get typed data
    bool TryGetData<T>(string key, out T value);        // Safe data access
    void ContinueProcessing();                          // Resume from WAITING
}
```

### LSBaseEvent Abstract Class

```csharp
public abstract class LSBaseEvent : ILSMutableEvent {
    // Simple processing
    public bool Process(LSDispatcher dispatcher);
    
    // Event-scoped processing with builder
    public bool ProcessWith<TEvent>(LSDispatcher dispatcher, 
        Func<LSEventCallbackBuilder<TEvent>, LSEventCallbackBuilder<TEvent>> configure) 
        where TEvent : class, ILSEvent;
}
```

### LSEvent\<TInstance\> Generic Class

```csharp
public abstract class LSEvent<TInstance> : LSBaseEvent where TInstance : class {
    public TInstance Instance { get; }                  // Strongly-typed event source
    protected LSEvent(TInstance instance);              // Constructor requires instance
}
```

## Phase-Based Processing

### LSEventPhase Enum

```csharp
[Flags]
public enum LSEventPhase {
    VALIDATE = 1,        // Input validation, permission checks
    PREPARE = 2,         // Setup, resource allocation  
    EXECUTE = 4,         // Core business logic
    SUCCESS = 8,         // Success-only operations (not when cancelled)
    CANCEL = 16,         // Cancellation cleanup (only when cancelled)
    COMPLETE = 32        // Always runs (logging, metrics)
}
```

### LSPhaseResult Enum

```csharp
public enum LSPhaseResult {
    CONTINUE,            // Continue to next handler/phase
    SKIP_REMAINING,      // Skip remaining handlers in this phase
    CANCEL,              // Cancel event (triggers CANCEL phase)
    RETRY,               // Retry handler execution
    WAITING              // Pause for async operation
}
```

### LSPhasePriority Enum

```csharp
public enum LSPhasePriority {
    CRITICAL = 0,        // System-critical operations (runs first)
    HIGH = 1,            // Important business logic
    NORMAL = 2,          // Standard operations (default)
    LOW = 3,             // Nice-to-have features
    BACKGROUND = 4       // Background operations (runs last)
}
```

## Event-Scoped Handlers

The `LSEventCallbackBuilder` provides a fluent API for registering handlers specific to a single event instance:

### Core Phase Methods

```csharp
public LSEventCallbackBuilder<TEvent> OnValidation(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL);
public LSEventCallbackBuilder<TEvent> OnPrepare(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL);
public LSEventCallbackBuilder<TEvent> OnExecution(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL);
public LSEventCallbackBuilder<TEvent> OnSuccess(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL);
public LSEventCallbackBuilder<TEvent> OnComplete(LSPhaseHandler<TEvent> handler, LSPhasePriority priority = LSPhasePriority.NORMAL);
```

### Conditional Handlers

```csharp
// State-based handler (runs during COMPLETE phase when error exists)
public LSEventCallbackBuilder<TEvent> OnError(LSPhaseHandler<TEvent> handler);
```

## Cancellation Handling

### OnCancel Method Overloads

The event system provides comprehensive cancellation support through multiple OnCancel overloads:

#### 1. Basic Cancel Handler

```csharp
.OnCancel(evt => {
    // Simple cleanup action
    evt.SetData("cleanup.performed", true);
})
```

#### 2. Priority-Based Execution

```csharp
.OnCancel(criticalCleanupAction, LSPhasePriority.CRITICAL)  // Runs first
.OnCancel(normalCleanupAction, LSPhasePriority.NORMAL)      // Runs second  
.OnCancel(backgroundLogAction, LSPhasePriority.LOW)         // Runs last
```

#### 3. Phase-Specific Cancellation

```csharp
// Only runs if cancelled during VALIDATE phase
.OnCancel(LSEventPhase.VALIDATE, evt => RollbackValidation(evt))

// Runs if cancelled during VALIDATE OR EXECUTE phases  
.OnCancel(LSEventPhase.VALIDATE | LSEventPhase.EXECUTE, evt => CommonCleanup(evt))
```

#### 4. Conditional Cancellation

```csharp
.OnCancelWhen(evt => CleanupDatabase(evt), 
              evt => evt.GetData<bool>("database.transaction.started"))
```

### Processing Flow with CANCEL Phase

```text
Normal Flow:    VALIDATE → PREPARE → EXECUTE → SUCCESS → COMPLETE
Cancelled Flow: [VALIDATE|PREPARE|EXECUTE] → CANCEL → COMPLETE
                    ↑                            ↑
              (cancellation)              (cleanup handlers)
```

## Asynchronous Processing

The event system supports sophisticated asynchronous operations through the `WAITING` state:

### WAITING State Features

- **Pause/Resume Control**: Handlers can return `LSPhaseResult.WAITING` to pause processing
- **Event-Controlled Resumption**: Only the event itself can resume via `ContinueProcessing()`
- **State Preservation**: Event maintains exact phase and handler position during wait
- **Thread-Safe Resumption**: `ContinueProcessing()` can be called from any thread

### Async Processing Pattern

```csharp
public LSPhaseResult HandleAsyncOperation(MyEvent evt, LSPhaseContext ctx) {
    // Start asynchronous operation
    Task.Run(async () => {
        await SomeAsyncWork();
        evt.SetData("async.result", "completed");
        evt.ContinueProcessing(); // Resume processing
    });
    
    return LSPhaseResult.WAITING; // Pause processing
}
```

### WAITING State Metadata

When entering WAITING state, the system automatically stores metadata:

```csharp
// Auto-generated metadata:
// "waiting.phase" (string)          - Phase where waiting started
// "waiting.handler.id" (string)     - ID of handler that initiated waiting  
// "waiting.started.at" (DateTime)   - Timestamp when waiting began
// "waiting.handler.count" (int)     - Number of handlers executed in phase
```

## Usage Examples

### Basic Event Processing

```csharp
// Define your event
public class UserRegistrationEvent : LSEvent<User> {
    public UserRegistrationEvent(User user) : base(user) { }
}

// Process with global handlers
var dispatcher = new LSDispatcher();
dispatcher.For<UserRegistrationEvent>()
    .InPhase(LSEventPhase.VALIDATE)
    .Register((evt, ctx) => ValidateUser(evt.Instance));

var userEvent = new UserRegistrationEvent(user);
var success = userEvent.Process(dispatcher);
```

### Event-Scoped Processing

```csharp
var userEvent = new UserRegistrationEvent(user);
var success = userEvent.ProcessWith<UserRegistrationEvent>(dispatcher, builder => builder
    .OnValidation((evt, ctx) => {
        if (string.IsNullOrEmpty(evt.Instance.Email)) {
            evt.SetErrorMessage("Email is required");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    })
    .OnExecution((evt, ctx) => {
        CreateUserAccount(evt.Instance);
        return LSPhaseResult.CONTINUE;
    })
    .OnSuccess((evt, ctx) => {
        SendWelcomeEmail(evt.Instance);
        return LSPhaseResult.CONTINUE;
    })
    .OnCancel(evt => {
        LogFailedRegistration(evt.Instance);
    })
    .OnComplete((evt, ctx) => {
        LogRegistrationAttempt(evt.Instance, evt.IsCancelled);
        return LSPhaseResult.CONTINUE;
    })
);
```

### Priority-Based Handlers

```csharp
var orderEvent = new OrderProcessingEvent(order);
var success = orderEvent.ProcessWith<OrderProcessingEvent>(dispatcher, builder => builder
    .OnValidation(SecurityValidation, LSPhasePriority.CRITICAL)      // Runs first
    .OnValidation(BusinessValidation, LSPhasePriority.NORMAL)        // Runs second
    .OnExecution(PaymentProcessing, LSPhasePriority.HIGH)            // High priority
    .OnExecution(InventoryUpdate, LSPhasePriority.NORMAL)            // Normal priority
    .OnComplete(MetricsLogging, LSPhasePriority.BACKGROUND)          // Runs last
);
```

### Async Processing with WAITING

```csharp
var paymentEvent = new PaymentProcessingEvent(payment);
var success = paymentEvent.ProcessWith<PaymentProcessingEvent>(dispatcher, builder => builder
    .OnExecution((evt, ctx) => {
        // Start async payment processing
        Task.Run(async () => {
            var result = await ProcessPaymentAsync(evt.Instance);
            evt.SetData("payment.result", result);
            evt.ContinueProcessing(); // Resume when complete
        });
        
        return LSPhaseResult.WAITING; // Pause until payment completes
    })
    .OnSuccess((evt, ctx) => {
        var result = evt.GetData<PaymentResult>("payment.result");
        SendConfirmationEmail(result);
        return LSPhaseResult.CONTINUE;
    })
);
```

### Sophisticated Cancellation Handling

```csharp
var dataProcessingEvent = new DataProcessingEvent(data);
var success = dataProcessingEvent.ProcessWith<DataProcessingEvent>(dispatcher, builder => builder
    .OnValidation((evt, ctx) => {
        if (!ValidateDataFormat(evt.Instance)) {
            evt.SetErrorMessage("Invalid data format");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    })
    .OnExecution((evt, ctx) => {
        evt.SetData("database.transaction.started", true);
        if (!ProcessData(evt.Instance)) {
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    })
    // Cleanup specific to validation failures
    .OnCancel(LSEventPhase.VALIDATE, evt => {
        LogValidationFailure(evt.Instance, evt.ErrorMessage);
    })
    // Cleanup specific to execution failures  
    .OnCancel(LSEventPhase.EXECUTE, evt => {
        RollbackDatabaseTransaction(evt.Instance);
    })
    // Conditional cleanup (only if transaction was started)
    .OnCancelWhen(evt => {
        CleanupDatabaseResources(evt.Instance);
    }, evt => evt.GetData<bool>("database.transaction.started"))
    // Always run on any cancellation
    .OnCancel(evt => {
        NotifyOperations(evt.Instance, "Data processing failed");
    }, LSPhasePriority.LOW)
);
```

## Best Practices

### ✅ Event Design

- **Single Responsibility**: Each event should represent one business operation
- **Immutable Data**: Avoid modifying the event's `Instance` property
- **Rich Context**: Include all necessary data in the event constructor

### ✅ Handler Design  

- **Phase Appropriate**: Place logic in the correct phase (validation in VALIDATE, etc.)
- **Priority Conscious**: Use priorities to control execution order when needed
- **Error Handling**: Use appropriate error handling and cancellation

### ✅ Cancellation

- **Critical First**: Use CRITICAL priority for security and resource cleanup
- **Conditional Cleanup**: Use `OnCancelWhen` for cleanup that depends on state
- **Phase-Specific**: Use phase-specific cancellation for targeted cleanup

### ✅ Async Operations

- **Always Resume**: Ensure `ContinueProcessing()` is called in all code paths
- **Error Handling**: Call `ContinueProcessing()` even when async operations fail
- **Timeout Handling**: Consider implementing timeouts for async operations

### ❌ Common Pitfalls

- **Don't** call `ContinueProcessing()` multiple times for one WAITING result
- **Don't** modify event data from multiple threads without synchronization
- **Don't** use WAITING for CPU-bound operations (use proper async/await patterns)
- **Don't** create circular dependencies between events
