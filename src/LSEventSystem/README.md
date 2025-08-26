# LSUtils Event System

A high-performance, phase-based event processing system with support for asynchronous operations, event-scoped handlers, and sophisticated cancellation handling.

## Table of Contents

- [Core Concepts](#core-concepts)
- [Event Types](#event-types)
- [Phase-Based Processing](#phase-based-processing)
- [Event-Scoped Handlers](#event-scoped-handlers)
- [Cancellation Handling](#cancellation-handling)
- [API Usage Examples](#api-usage-examples)
- [API Changes and Migration Guide](#api-changes-and-migration-guide)

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
}
```

### LSBaseEvent Abstract Class

```csharp
public abstract class LSBaseEvent : ILSMutableEvent {
    // Event-scoped builder (recommended)
    public LSEventCallbackBuilder<TEventType> Build<TEventType>(LSDispatcher dispatcher) 
        where TEventType : ILSEvent;
}
```

### LSDispatcher

```csharp
public class LSDispatcher {
    // Global handler registration
    public LSEventRegistration<TEvent> Build<TEvent>() where TEvent : ILSEvent;
    
    // Event processing (internal - use Build().Dispatch() pattern)
    internal bool ProcessEvent<TEvent>(TEvent eventInstance) where TEvent : ILSEvent;
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

// Processing method
public bool Dispatch();  // Renamed from ProcessAndCleanup
```

### State-Based Handlers

```csharp
// State-based handlers for simple actions
public LSEventCallbackBuilder<TEvent> OnError(LSAction<TEvent> action);
public LSEventCallbackBuilder<TEvent> OnSuccess(LSAction<TEvent> action);
public LSEventCallbackBuilder<TEvent> OnCancel(LSAction<TEvent> action);
```

## Cancellation Handling

### OnCancel Method

The event system provides simple cancellation support through the OnCancel method:

#### Basic Cancel Handler

```csharp
.OnCancel(evt => {
    // Simple cleanup action
    evt.SetData("cleanup.performed", true);
})
```

For complex cancellation logic with conditions or phase-specific handling, use OnComplete with your own conditional logic:

```csharp
.OnComplete((evt, ctx) => {
    if (evt.IsCancelled) {
        // Check which phase was cancelled
        if (evt.TryGetData<string>("cancel.phase", out var phase)) {
            if (phase == "VALIDATE") {
                // Handle validation cancellation
            } else if (phase == "EXECUTE") {
                // Handle execution cancellation  
            }
        }
        
        // Conditional cleanup
        if (evt.GetData<bool>("database.transaction.started")) {
            RollbackTransaction(evt);
        }
    }
    return LSPhaseResult.CONTINUE;
})
```

### Processing Flow with CANCEL Phase

```text
Normal Flow:    VALIDATE → PREPARE → EXECUTE → SUCCESS → COMPLETE
Cancelled Flow: [VALIDATE|PREPARE|EXECUTE] → CANCEL → COMPLETE
                    ↑                            ↑
              (cancellation)              (cleanup handlers)
```

## API Usage Examples

```csharp
// Define your event
public class UserRegistrationEvent : LSEvent<User> {
    public UserRegistrationEvent(User user) : base(user) { }
}

// Process with global handlers
var dispatcher = new LSDispatcher();
dispatcher.Build<UserRegistrationEvent>()
    .InPhase(LSEventPhase.VALIDATE)
    .Register((evt, ctx) => ValidateUser(evt.Instance));

var userEvent = new UserRegistrationEvent(user);
var success = userEvent.Build<UserRegistrationEvent>(dispatcher).Dispatch();
```

### Event-Scoped Processing

```csharp
// Recommended API for event-specific handlers
var userEvent = new UserRegistrationEvent(user);
var success = userEvent.Build<UserRegistrationEvent>(dispatcher)
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
    .OnSuccess(evt => {
        SendWelcomeEmail(evt.Instance);
    })
    .OnCancel(evt => {
        LogFailedRegistration(evt.Instance);
    })
    .OnError(evt => {
        SendErrorNotification(evt.Instance);
    })
    .OnComplete((evt, ctx) => {
        LogRegistrationAttempt(evt.Instance, evt.IsCancelled);
        return LSPhaseResult.CONTINUE;
    })
    .Dispatch();
```

### Priority-Based Handlers

```csharp
var orderEvent = new OrderProcessingEvent(order);
var success = orderEvent.Build<OrderProcessingEvent>(dispatcher)
    .OnValidation(SecurityValidation, LSPhasePriority.CRITICAL)      // Runs first
    .OnValidation(BusinessValidation, LSPhasePriority.NORMAL)        // Runs second
    .OnExecution(PaymentProcessing, LSPhasePriority.HIGH)            // High priority
    .OnExecution(InventoryUpdate, LSPhasePriority.NORMAL)            // Normal priority
    .OnComplete(MetricsLogging, LSPhasePriority.BACKGROUND)          // Runs last
    .Dispatch();
```

### Async Processing Support

```csharp
var paymentEvent = new PaymentProcessingEvent(payment);
var success = paymentEvent.Build<PaymentProcessingEvent>(dispatcher)
    .OnExecution((evt, ctx) => {
        // Start async payment processing
        Task.Run(async () => {
            try {
                var result = await ProcessPaymentAsync(evt.Instance);
                evt.SetData("payment.result", result);
                evt.Resume(); // Resume processing after async operation
            } catch (Exception ex) {
                evt.SetErrorMessage(ex.Message);
                evt.Resume(); // Always resume, even on error
            }
        });
        
        return LSPhaseResult.WAITING; // Pause processing until Resume() is called
    })
    .OnSuccess(evt => {
        var result = evt.GetData<PaymentResult>("payment.result");
        SendConfirmationEmail(result);
    })
    .OnError(evt => {
        LogPaymentError(evt.ErrorMessage);
    })
    .Dispatch();
```

### Simplified Event Processing

```csharp
var dataProcessingEvent = new DataProcessingEvent(data);
var success = dataProcessingEvent.Build<DataProcessingEvent>(dispatcher)
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
    .OnCancel(evt => {
        // Simple cancellation cleanup
        LogFailedProcessing(evt.Instance, evt.ErrorMessage);
        if (evt.GetData<bool>("database.transaction.started")) {
            RollbackDatabaseTransaction(evt.Instance);
        }
        NotifyOperations(evt.Instance, "Data processing failed");
    })
    .OnError(evt => {
        SendErrorAlert(evt.Instance);
    })
    .OnSuccess(evt => {
        SendSuccessNotification(evt.Instance);
    })
    .Dispatch();
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

- **Simple Cleanup**: Use `OnCancel` for basic cleanup actions
- **Conditional Logic**: Use `OnComplete` with conditional logic for complex cancellation scenarios
- **Event Data**: Use event data to track state and make cleanup decisions

### ✅ Async Operations

- **Always Resume**: Ensure `Resume()` is called in all code paths
- **Error Handling**: Call `Resume()` even when async operations fail
- **Timeout Handling**: Consider implementing timeouts for async operations

### ❌ Common Pitfalls

- **Don't** call `Resume()` multiple times for one WAITING result
- **Don't** modify event data from multiple threads without synchronization
- **Don't** use WAITING for CPU-bound operations (use proper async/await patterns)
- **Don't** create circular dependencies between events

## API Changes and Migration Guide

## API Simplification

The LSEventSystem has been simplified with cleaner method names and better consistency:

### Method Name Changes

| Old Method | New Method | Notes |
|------------|------------|-------|
| `RegisterCallback<T>()` | `Build<T>()` | Consistent naming across dispatcher and events |
| `ProcessAndCleanup()` | `Dispatch()` | Clearer intent for execution |
| `For<T>()` | `Build<T>()` | Unified builder pattern |
| `ProcessWith<T>()` | `Build<T>().Dispatch()` | Split into build + dispatch pattern |

#### Custom Delegate

- **LSEventCondition\<TEvent\>**: Replaces `Func<TEvent, bool>` in APIs to provide clearer intent

### Migration Examples

#### Dispatcher Handler Registration

```csharp
// New (recommended)
dispatcher.Build<MyEvent>()
    .InPhase(LSEventPhase.VALIDATE)
    .Register(handler);
```

### Event-Scoped Processing Pattern

```csharp
// New (recommended)
var success = myEvent.Build<MyEvent>(dispatcher)
    .OnValidation(handler)
    .OnExecution(handler2)
    .Dispatch();
```

#### Manual Builder Usage

```csharp
// New (recommended)
var builder = myEvent.Build<MyEvent>(dispatcher);
builder.OnExecution(handler);
var success = builder.Dispatch();
```
