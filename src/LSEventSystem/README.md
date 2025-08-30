# LSUtils Event System

A high-performance, phase-based event processing system with support for asynchronous operations, event-scoped handlers, and sophisticated failure management.

## Table of Contents

- [Core Concepts](#core-concepts)
- [Phase-Based Processing](#phase-based-processing)
- [Event Types and Interfaces](#event-types-and-interfaces)
- [Event-Scoped Handlers](#event-scoped-handlers)
- [Global Handler Registration](#global-handler-registration)
- [Asynchronous Operations](#asynchronous-operations)
- [Priority System](#priority-system)
- [Usage Examples](#usage-examples)
  - [Basic Attribute Management](#basic-attribute-management)
  - [Complex Validation Scenarios](#complex-validation-scenarios)
  - [Asynchronous Loading Operations](#asynchronous-loading-operations)
  - [Transaction-Like Operations](#transaction-like-operations)
- [Best Practices](#best-practices)

## Core Concepts

The LSEventSystem is built around **phase-based processing** where events flow through well-defined phases with strict execution order. This provides predictable event handling, sophisticated error management, and support for asynchronous operations.

### Event Processing Flow

Events follow one of three possible execution paths:

```text
✅ Standard Flow (Success):
VALIDATE → PREPARE → EXECUTE → SUCCESS → COMPLETE

❌ Failure Flow (Recoverable Error):
VALIDATE → PREPARE → EXECUTE → FAILURE → COMPLETE

🚫 Cancellation Flow (Critical Error):
[VALIDATE|PREPARE|EXECUTE] → CANCEL → COMPLETE
```

### Phase Purposes

Each phase serves a specific purpose in the event lifecycle:

- **VALIDATE** (1): Input validation, permission checks, early validation
- **PREPARE** (2): Setup, resource allocation, state preparation  
- **EXECUTE** (4): Core business logic and main processing
- **SUCCESS** (8): Success-only operations, result processing
- **FAILURE** (16): Failure handling and recovery logic
- **CANCEL** (32): Cancellation cleanup, rollback operations
- **COMPLETE** (64): Always runs regardless of outcome (logging, cleanup)

## Phase-Based Processing

### LSEventPhase Enum

The phase system uses bitwise flags for tracking completion:

```csharp
[Flags]
public enum LSEventPhase {
    VALIDATE = 1,        // Input validation and early checks
    PREPARE = 2,         // Resource allocation and setup
    EXECUTE = 4,         // Core business logic
    SUCCESS = 8,         // Success-only post-processing
    FAILURE = 16,        // Failure handling and recovery
    CANCEL = 32,         // Cancellation cleanup
    COMPLETE = 64        // Always runs (logging, cleanup)
}
```

### LSPhaseResult Enum

Handlers return results that control event flow:

```csharp
public enum LSPhaseResult {
    CONTINUE,            // Continue to next handler/phase
    SKIP_REMAINING,      // Skip remaining handlers in current phase
    CANCEL,              // Cancel event (triggers CANCEL phase)
    RETRY,               // Retry current handler (with retry policies)
    WAITING              // Pause for async operation
}
```

### LSPhasePriority Enum

Controls execution order within phases (lower values execute first):

```csharp
public enum LSPhasePriority {
    CRITICAL = 0,        // System-critical operations
    HIGH = 1,            // Important business logic
    NORMAL = 2,          // Standard operations (default)
    LOW = 3,             // Nice-to-have features
    BACKGROUND = 4       // Background operations
}
```

## Event Types and Interfaces

### ILSEvent Interface (Public API)

The core interface that all events implement:

```csharp
public interface ILSEvent {
    Guid ID { get; }                                    // Unique event identifier
    DateTime CreatedAt { get; }                         // Creation timestamp
    bool IsCancelled { get; }                           // Cancellation status
    bool HasFailures { get; }                           // Failure status
    bool IsCompleted { get; }                           // Completion status
    LSEventPhase CurrentPhase { get; }                  // Current processing phase
    LSEventPhase CompletedPhases { get; }               // Bitwise flags of completed phases
    
    // Data access methods
    bool TryGetData<T>(string key, out T value);        // Safe data access
    T GetData<T>(string key);                           // Direct data access (may throw)
    void SetData<T>(string key, T value);               // Store data in event
}
```

### ILSMutableEvent Interface (Internal)

Extended interface used internally by the dispatcher:

```csharp
internal interface ILSMutableEvent : ILSEvent {
    new bool IsCancelled { get; set; }                  // Mutable cancellation state
    new bool HasFailures { get; set; }                  // Mutable failure state
    new bool IsCompleted { get; set; }                  // Mutable completion state
    new LSEventPhase CurrentPhase { get; set; }         // Mutable current phase
    new LSEventPhase CompletedPhases { get; set; }      // Mutable completed phases
    bool IsWaiting { get; set; }                        // Async operation waiting state
    
    // Async operation control
    void Resume();                                      // Complete async operation successfully
    void Abort();                                       // Cancel async operation (critical failure)
    void Fail();                                        // Mark async operation as failed (recoverable)
}
```

### LSBaseEvent Abstract Class

Base implementation providing complete event functionality:

```csharp
public abstract class LSBaseEvent : ILSEvent, ILSMutableEvent {
    // Core properties implemented
    public Guid ID { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public bool IsCancelled { get; set; }
    public bool HasFailures { get; set; }
    public bool IsCompleted { get; set; }
    public LSEventPhase CurrentPhase { get; set; }
    public LSEventPhase CompletedPhases { get; set; }
    public bool IsWaiting { get; set; }
    
    // Event-scoped handler registration
    public LSEventCallbackBuilder<TEvent> WithCallbacks<TEvent>(LSDispatcher dispatcher) 
        where TEvent : LSBaseEvent;
    
    // Async operation control
    public void Resume();
    public void Abort();
    public void Fail();
    
    // Data storage and retrieval
    public bool TryGetData<T>(string key, out T value);
    public T GetData<T>(string key);
    public void SetData<T>(string key, T value);
}
```

### LSEvent< TInstance > Generic Class

For events tied to a specific source object:

```csharp
public abstract class LSEvent<TInstance> : LSBaseEvent where TInstance : class {
    public TInstance Instance { get; }                  // Strongly-typed event source
    
    protected LSEvent(TInstance instance) {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
    }
}
```

## Event-Scoped Handlers

The `LSEventCallbackBuilder<TEvent>` provides a fluent API for registering handlers specific to a single event instance. This is the primary way to handle events in the LSEventSystem.

### Core Phase Handler Methods

```csharp
public class LSEventCallbackBuilder<TEvent> where TEvent : ILSEvent {
    // Phase-specific handlers (with full context)
    public LSEventCallbackBuilder<TEvent> OnValidatePhase(
        LSPhaseHandler<TEvent> handler, 
        LSPhasePriority priority = LSPhasePriority.NORMAL);
        
    public LSEventCallbackBuilder<TEvent> OnPreparePhase(
        LSPhaseHandler<TEvent> handler, 
        LSPhasePriority priority = LSPhasePriority.NORMAL);
        
    public LSEventCallbackBuilder<TEvent> OnExecutePhase(
        LSPhaseHandler<TEvent> handler, 
        LSPhasePriority priority = LSPhasePriority.NORMAL);
        
    public LSEventCallbackBuilder<TEvent> OnSuccessPhase(
        LSPhaseHandler<TEvent> handler, 
        LSPhasePriority priority = LSPhasePriority.NORMAL);
    public LSEventCallbackBuilder<TEvent> OnFailurePhase(
        LSPhaseHandler<TEvent> handler, 
        LSPhasePriority priority = LSPhasePriority.NORMAL);
    public LSEventCallbackBuilder<TEvent> OnCancelPhase(
        LSPhaseHandler<TEvent> handler, 
        LSPhasePriority priority = LSPhasePriority.NORMAL);
    public LSEventCallbackBuilder<TEvent> OnCompletePhase(
        LSPhaseHandler<TEvent> handler, 
        LSPhasePriority priority = LSPhasePriority.NORMAL);
}
```

### Phase Handler Signature

```csharp
public delegate LSPhaseResult LSPhaseHandler<in TEvent>(TEvent @event, LSPhaseContext context) 
    where TEvent : ILSEvent;
```

### Simplified Action Handlers

For simple operations that don't need full context:

```csharp
// Single parameter action handlers
public LSEventCallbackBuilder<TEvent> OnSuccess(LSAction<TEvent> action, 
    LSPhasePriority priority = LSPhasePriority.NORMAL);
public LSEventCallbackBuilder<TEvent> OnFailure(LSAction<TEvent> action, 
    LSPhasePriority priority = LSPhasePriority.NORMAL);
public LSEventCallbackBuilder<TEvent> OnCancel(LSAction<TEvent> action, 
    LSPhasePriority priority = LSPhasePriority.NORMAL);
public LSEventCallbackBuilder<TEvent> OnComplete(LSAction<TEvent> action, 
    LSPhasePriority priority = LSPhasePriority.NORMAL);
public LSEventCallbackBuilder<TEvent> OnError(LSAction<TEvent> action);     // Handles both failure and cancel
```

### Conditional Handlers

```csharp
// Conditional processing
public LSEventCallbackBuilder<TEvent> CancelIf(
    LSEventCondition<TEvent> condition, 
    string errorMessage = "", 
    LSEventPhase phase = LSEventPhase.VALIDATE);
    
public LSEventCallbackBuilder<TEvent> FailIf(
    LSEventCondition<TEvent> condition, 
    string errorMessage = "", 
    LSEventPhase phase = LSEventPhase.VALIDATE);
```

### Event Condition Signature

```csharp
public delegate bool LSEventCondition<in TEvent>(TEvent @event) where TEvent : ILSEvent;
```

### Processing Methods

```csharp
// Execute the event through all phases
public bool Dispatch();  // Returns true if completed successfully, false if cancelled/failed
```

## Global Handler Registration

The `LSEventHandlerBuilder<TEvent>` provides fluent API for registering handlers globally that apply to all events of a specific type.

### LSEventHandlerBuilder Methods

```csharp
public class LSEventHandlerBuilder<TEvent> where TEvent : ILSEvent {
    // Phase configuration
    public LSEventHandlerBuilder<TEvent> InPhase(LSEventPhase phase);
    
    // Priority configuration
    public LSEventHandlerBuilder<TEvent> WithPriority(LSPhasePriority priority);
    
    // Instance restriction
    public LSEventHandlerBuilder<TEvent> ForInstance<T>(T instance) where T : class;
    
    // Execution limits
    public LSEventHandlerBuilder<TEvent> MaxExecutions(int count);
    
    // Conditional execution
    public LSEventHandlerBuilder<TEvent> When(LSEventCondition<TEvent> condition);
    
    // Registration
    public Guid Register(LSPhaseHandler<TEvent> handler);
    
    // Unregistration
    public int Unregister();
    public int Unregister(LSPhaseHandler<TEvent> handler);
    public bool UnregisterById(Guid handlerId);
}
```

### LSDispatcher Usage

```csharp
public class LSDispatcher {
    // Global handler registration
    public LSEventHandlerBuilder<TEvent> ForEvent<TEvent>() where TEvent : ILSEvent;
    
    // Direct handler registration (internal)
    internal Guid RegisterHandler<TEvent>(
        LSPhaseHandler<TEvent> handler,
        LSEventPhase phase = LSEventPhase.EXECUTE,
        LSPhasePriority priority = LSPhasePriority.NORMAL,
        Type? instanceType = null,
        object? instance = null,
        int maxExecutions = -1,
        LSEventCondition<TEvent>? condition = null) where TEvent : ILSEvent;
        
    // Event processing
    internal bool ProcessEvent<TEvent>(TEvent @event) where TEvent : ILSEvent;
    internal bool ContinueProcessing<TEvent>(TEvent @event) where TEvent : ILSEvent;
    
    // Handler management
    public bool UnregisterHandler(Guid handlerId);
    public int GetHandlerCount<TEvent>() where TEvent : ILSEvent;
    public int GetTotalHandlerCount();
}
```

## Priority System

### Execution Order

Within each phase, handlers execute in priority order:

1. **CRITICAL** (0) - System-critical operations, security checks
2. **HIGH** (1) - Important business logic
3. **NORMAL** (2) - Standard operations (default)
4. **LOW** (3) - Nice-to-have features
5. **BACKGROUND** (4) - Logging, metrics, cleanup

### Examples

```csharp
// Critical validation runs first
.OnValidatePhase(SecurityCheck, LSPhasePriority.CRITICAL)
.OnValidatePhase(BusinessValidation, LSPhasePriority.NORMAL)
.OnValidatePhase(OptionalChecks, LSPhasePriority.LOW)
```

## Asynchronous Operations

The LSEventSystem provides robust support for asynchronous operations through the WAITING state and resumption control methods.

### WAITING State

When a handler returns `LSPhaseResult.WAITING`, the event pauses and waits for external signal:

```csharp
.OnExecutePhase((evt, ctx) => {
    // Start async operation
    StartDatabaseOperation(evt, (result) => {
        if (result.Success) {
            evt.Resume();       // Continue successfully
        } else if (result.IsCritical) {
            evt.Abort();        // Critical failure - cancel event
        } else {
            evt.Fail();         // Non-critical failure - trigger FAILURE phase
        }
    });
    
    return LSPhaseResult.WAITING;  // Pause event processing
})
```

### Resumption Methods

Events provide three methods to control async operation completion (only valid when event is in WAITING state):

```csharp
public abstract class LSBaseEvent : ILSEvent {
    // Complete async operation successfully - continue to next phase
    public void Resume();
    
    // Critical failure - immediately jump to CANCEL phase
    public void Abort();
    
    // Non-critical failure - mark HasFailures=true, trigger FAILURE phase after current phase
    public void Fail();
}
```

**Important**: These methods should ONLY be called when the event is in WAITING state (after a handler returned `LSPhaseResult.WAITING`). They are NOT for use during normal synchronous phase execution.

### Async Flow Examples

```csharp
// Success path: WAITING → Resume() → Continue to next phase
.OnExecutePhase((evt, ctx) => {
    ApiClient.LoadDataAsync(evt.ID, (data) => {
        evt.SetData("loaded_data", data);
        evt.Resume();  // Continue to SUCCESS phase
    });
    return LSPhaseResult.WAITING;
})

// Failure path: WAITING → Fail() → FAILURE phase
.OnExecutePhase((evt, ctx) => {
    ApiClient.LoadDataAsync(evt.ID, (data) => {
        if (data == null) {
            evt.SetData("error", "Data not found");
            evt.Fail();  // Go to FAILURE phase for recovery
        } else {
            evt.SetData("loaded_data", data);
            evt.Resume();  // Continue to SUCCESS phase
        }
    });
    return LSPhaseResult.WAITING;
})

// Critical failure path: WAITING → Abort() → CANCEL phase
.OnExecutePhase((evt, ctx) => {
    DatabaseConnection.ExecuteAsync(evt.GetData<string>("sql"), (result) => {
        if (result.IsConnectionLost) {
            evt.SetData("critical_error", "Database connection lost");
            evt.Abort();  // Critical failure - go directly to CANCEL
        } else {
            evt.Resume();
        }
    });
    return LSPhaseResult.WAITING;
})
```

## Usage Examples

### Basic Attribute Management

#### Simple Attribute Initialization

```csharp
public class WHAttributeInitializeEvent : LSEvent<IWHAttribute> {
    public IWHAttribute Attribute => Instance;
    public object? OwnerInstance { get; }

    protected WHAttributeInitializeEvent(IWHAttribute attribute, object? ownerInstance) 
        : base(attribute) {
        OwnerInstance = ownerInstance;
    }

    public static WHAttributeInitializeEvent Create(IWHAttribute attribute, object? ownerInstance) {
        return new WHAttributeInitializeEvent(attribute, ownerInstance);
    }
}

// Usage with event-scoped handlers
WHAttributeInitializeEvent.Create(this, options.OwnerInstance)
    .WithCallbacks<WHAttributeInitializeEvent>(dispatcher)
    .CancelIf((evt) => evt.Attribute.IsInitialized, "already_initialized")
    .OnSuccess((evt) => {
        evt.Attribute._dispatcher = evt.Dispatcher;
        evt.Attribute.OwnerInstance = evt.OwnerInstance;
        evt.Attribute.IsInitialized = true;
    })
    .Dispatch();
```

#### Attribute Value Changes with Validation

```csharp
public class WHAttributeValueChangedEvent : LSEvent<IWHAttribute> {
    public IWHAttribute Attribute => Instance;
    public object OldValue { get; }
    public object NewValue { get; }

    protected WHAttributeValueChangedEvent(IWHAttribute attribute, object oldValue, object newValue) 
        : base(attribute) {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public static WHAttributeValueChangedEvent Create(IWHAttribute attribute, object oldValue, object newValue) {
        return new WHAttributeValueChangedEvent(attribute, oldValue, newValue);
    }
}

// Usage with conditional cancellation and validation
WHAttributeValueChangedEvent.Create(this, BaseValue, value)
    .WithCallbacks<WHAttributeValueChangedEvent>(_dispatcher)
    .CancelIf((evt) => evt.Attribute.IsReadOnly, "attribute_read_only")
    .CancelIf((evt) => evt.OldValue.Equals(evt.NewValue), "no_value_change")
    .OnValidatePhase((evt, ctx) => {
        // Custom validation logic
        if (evt.NewValue is int intValue && intValue < 0) {
            evt.SetData("error.message", "Value cannot be negative");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    })
    .OnSuccess((evt) => {
        BaseValue = (T)evt.NewValue;
        NotifyValueChanged();
    })
    .Dispatch();
```

### Complex Validation Scenarios

#### Multi-Phase Validation with Priority

```csharp
public class WHConstrainedAttributeInitializeEvent : LSEvent<WHConstrainedAttribute<T>> {
    public WHConstrainedAttribute<T> Attribute => Instance;
    public T MinValue { get; }
    public T MaxValue { get; }

    // Constructor...
}

// Complex initialization with multiple validation phases
WHConstrainedAttributeInitializeEvent.Create(this, minValue, maxValue)
    .WithCallbacks<WHConstrainedAttributeInitializeEvent>(dispatcher)
    // Critical system validation first
    .OnValidatePhase((evt, ctx) => {
        if (evt.Attribute.IsInitialized) {
            evt.SetData("error", "Attribute already initialized");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    }, LSPhasePriority.CRITICAL)
    
    // Business rule validation
    .OnValidatePhase((evt, ctx) => {
        if (evt.MinValue.CompareTo(evt.MaxValue) > 0) {
            evt.SetData("error", "MinValue cannot be greater than MaxValue");
            return LSPhaseResult.CANCEL;
        }
        return LSPhaseResult.CONTINUE;
    }, LSPhasePriority.NORMAL)
    
    // Constraint setup in prepare phase
    .OnPreparePhase((evt, ctx) => {
        evt.Attribute._minConstraint = new WHAttributeConstraint<T>(evt.MinValue, WHConstraintType.Minimum);
        evt.Attribute._maxConstraint = new WHAttributeConstraint<T>(evt.MaxValue, WHConstraintType.Maximum);
        return LSPhaseResult.CONTINUE;
    })
    
    // Final initialization
    .OnSuccess((evt) => {
        evt.Attribute.IsInitialized = true;
    })
    .Dispatch();
```

### Asynchronous Loading Operations

#### Attribute Loading with Complex Validation and Data Parsing

```csharp
public class WHAttributeLoadRequestEvent : LSBaseEvent {
    public IWHAttribute? Attribute { get; set; }
    public string SerializedData { get; }
    public ILSSerializer Serializer { get; }

    // Constructor...
}

// Complex async loading based on the WHAttribute.Load method
public static void Load(WHAttribute.WHAttributeEventOptions options) {
    System.Guid id = Guid.Empty;
    string label = string.Empty;
    T baseValue = default!;
    bool isReadOnly = false;

    WHAttributeLoadRequestEvent.CreateWithCallback(options)
        // Validation phase - check required data
        .CancelIf((evt) => string.IsNullOrEmpty(evt.SerializedData), "missing_serialized_data")
        .CancelIf((evt) => evt.Serializer == null, "missing_serializer")
        
        // Prepare phase - deserialize data
        .OnPreparePhase((evt, ctx) => {
            try {
                var data = evt.Serializer.Deserialize<Dictionary<string, string>>(evt.SerializedData);
                evt.SetData("deserialized_data", data);
                return LSPhaseResult.CONTINUE;
            } catch (Exception ex) {
                evt.SetData("error", $"Deserialization failed: {ex.Message}");
                return LSPhaseResult.CANCEL;
            }
        })
        
        // Validation of deserialized data using WHAttribute.Sanitize
        .OnValidatePhase((evt, ctx) => {
            var data = evt.GetData<Dictionary<string, string>>("deserialized_data");
            
            if (!WHAttribute.Sanitize<System.Guid>(data, "id", out id)) {
                evt.SetData("error", "missing_or_invalid_id");
                return LSPhaseResult.CANCEL;
            }
            
            if (!WHAttribute.Sanitize<string>(data, "label", out label)) {
                evt.SetData("error", "missing_or_invalid_label");
                return LSPhaseResult.CANCEL;
            }
            
            if (!WHAttribute.Sanitize<T>(data, "base_value", out baseValue)) {
                evt.SetData("error", "missing_or_invalid_base_value");
                return LSPhaseResult.CANCEL;
            }
            
            if (!WHAttribute.Sanitize<bool>(data, "is_read_only", out isReadOnly)) {
                evt.SetData("error", "missing_or_invalid_is_read_only");
                return LSPhaseResult.CANCEL;
            }
            
            return LSPhaseResult.CONTINUE;
        })
        
        // Execute phase - create attribute instance
        .OnExecutePhase((evt, ctx) => {
            evt.Attribute = new WHAttribute<T>(id, label, baseValue, isReadOnly);
            return evt.Attribute != null ? LSPhaseResult.CONTINUE : LSPhaseResult.CANCEL;
        })
        
        // Success callback
        .OnSuccess((evt) => {
            options.OnSuccessCallback?.Invoke(evt);
        })
        
        // Error handling
        .OnCancel((evt) => {
            var error = evt.TryGetData<string>("error", out var errorMsg) ? errorMsg : "unknown_error";
            evt.SetData("final_error", error);
            options.OnCancelCallback?.Invoke(evt);
        })
        
        .Dispatch();
}
```

### Transaction-Like Operations

#### Database Transaction Pattern

```csharp
public class DatabaseTransactionEvent : LSBaseEvent {
    public string ConnectionString { get; }
    public List<string> SqlCommands { get; }
    public object? Transaction { get; set; }
    public List<object> Results { get; } = new();

    // Constructor...
}

// Transaction pattern with rollback support
DatabaseTransactionEvent.Create(connectionString, sqlCommands)
    .WithCallbacks<DatabaseTransactionEvent>(dispatcher)
    
    // Prepare phase - open connection and begin transaction
    .OnPreparePhase((evt, ctx) => {
        try {
            var connection = new SqlConnection(evt.ConnectionString);
            connection.Open();
            evt.Transaction = connection.BeginTransaction();
            evt.SetData("connection", connection);
            return LSPhaseResult.CONTINUE;
        } catch (Exception ex) {
            evt.SetData("error", $"Failed to start transaction: {ex.Message}");
            return LSPhaseResult.CANCEL;
        }
    })
    
    // Execute phase - run all commands
    .OnExecutePhase((evt, ctx) => {
        var connection = evt.GetData<SqlConnection>("connection");
        var transaction = (SqlTransaction)evt.Transaction!;
        
        try {
            foreach (var sql in evt.SqlCommands) {
                var command = new SqlCommand(sql, connection, transaction);
                var result = command.ExecuteScalar();
                evt.Results.Add(result);
            }
            return LSPhaseResult.CONTINUE;
        } catch (Exception ex) {
            evt.SetData("execution_error", ex.Message);
            evt.Fail(); // Mark as failed but allow recovery in FAILURE phase
            return LSPhaseResult.CONTINUE;
        }
    })
    
    // Success phase - commit transaction
    .OnSuccessPhase((evt, ctx) => {
        var transaction = (SqlTransaction)evt.Transaction!;
        transaction.Commit();
        return LSPhaseResult.CONTINUE;
    })
    
    // Failure phase - rollback transaction
    .OnFailurePhase((evt, ctx) => {
        var transaction = (SqlTransaction)evt.Transaction!;
        transaction.Rollback();
        
        // Attempt recovery or alternative operation
        var error = evt.GetData<string>("execution_error");
        evt.SetData("recovery_attempted", true);
        return LSPhaseResult.CONTINUE;
    })
    
    // Cancel phase - emergency cleanup
    .OnCancelPhase((evt, ctx) => {
        if (evt.Transaction is SqlTransaction transaction) {
            try {
                transaction.Rollback();
            } catch {
                // Emergency cleanup - ignore rollback errors
            }
        }
        return LSPhaseResult.CONTINUE;
    })
    
    // Complete phase - always close connection
    .OnCompletePhase((evt, ctx) => {
        if (evt.TryGetData<SqlConnection>("connection", out var connection)) {
            connection?.Close();
            connection?.Dispose();
        }
        return LSPhaseResult.CONTINUE;
    })
    
    .Dispatch();
```

## Best Practices

### Event Design Principles

1. **Single Responsibility**: Each event should represent one logical operation
2. **Immutable Event Data**: Don't modify event properties after creation
3. **Descriptive Names**: Use clear, descriptive names for events and properties
4. **Type Safety**: Use strongly-typed events with `LSEvent<TInstance>` when possible

### Phase Usage Guidelines

- **VALIDATE**: Use for input validation, permission checks, and early validation
- **PREPARE**: Use for resource allocation, state setup, and initialization
- **EXECUTE**: Use for core business logic and main processing
- **SUCCESS**: Use for side effects, notifications, and success-only operations
- **FAILURE**: Use for recovery logic, fallback operations, and graceful degradation
- **CANCEL**: Use for cleanup, rollback, and error handling
- **COMPLETE**: Use for logging, metrics, and guaranteed cleanup operations

### Error Handling Best Practices

```csharp
// ✅ Good: Distinguish between critical and recoverable errors
.OnExecutePhase((evt, ctx) => {
    try {
        ProcessBusinessLogic(evt);
        return LSPhaseResult.CONTINUE;
    } catch (SecurityException ex) {
        evt.SetData("security_error", ex.Message);
        return LSPhaseResult.CANCEL;  // Critical - stop immediately, go to CANCEL phase
    } catch (ValidationException ex) {
        evt.SetData("validation_error", ex.Message);
        return LSPhaseResult.FAILURE; // Mark as failed - will trigger FAILURE phase after EXECUTE
    }
})

// ✅ Use FAILURE phase handler for recovery logic
.OnFailurePhase((evt, ctx) => {
    // Recovery logic when LSPhaseResult.FAILURE was returned or HasFailures is true
    var error = evt.GetData<string>("validation_error");
    HandleRecoverableError(evt, error);
    return LSPhaseResult.CONTINUE;
})

// ✅ Use retry for transient errors
.RetryOnError((evt, ctx) => {
    // Handler logic that may throw
    return LSPhaseResult.CONTINUE;
}, maxRetries: 3)

// ❌ Bad: Treating all errors the same way
.OnExecutePhase((evt, ctx) => {
    try { ProcessBusinessLogic(evt); return LSPhaseResult.CONTINUE; }
    catch { return LSPhaseResult.CANCEL; }
})

// ❌ Bad: Using evt.Fail() during synchronous phase execution
.OnExecutePhase((evt, ctx) => {
    evt.Fail(); // Only valid in async WAITING state
    return LSPhaseResult.CONTINUE;
})
```

### Failure vs Retry: When to Use Each

**Failure Handling (OnFailurePhase/OnFailure):**

- Use when you want to handle or recover from a failed event (e.g., fallback logic, logging, user notification).
- Triggered when a handler returns `LSPhaseResult.FAILURE` or the event is marked as failed.
- Allows for graceful degradation and recovery.

**Retry Handling (RetryOnError):**

- Use when you want to automatically retry a handler on transient errors (e.g., network issues, temporary unavailability).
- Retries the same handler up to a maximum count before failing.
- If all retries fail, the event can then proceed to FAILURE phase for recovery.

**Are they redundant?**

- No: Retry is for automatic, programmatic attempts to resolve transient issues; Failure handling is for recovery after all attempts have failed.
- Best practice: Use RetryOnError for transient errors, and OnFailurePhase/OnFailure for fallback or user-facing recovery.

**Example:**

```csharp
.OnExecutePhase((evt, ctx) => {
    // ...
    if (SomeRecoverableError)
        return LSPhaseResult.FAILURE;
    return LSPhaseResult.CONTINUE;
})
.OnFailurePhase((evt, ctx) => {
    // Fallback logic
    return LSPhaseResult.CONTINUE;
})
.RetryOnError((evt, ctx) => {
    // Handler logic
    return LSPhaseResult.CONTINUE;
}, maxRetries: 2)
```

### Async Operation Guidelines

- **Use WAITING** for I/O-bound operations (database, file, network)
- **Don't use WAITING** for CPU-bound operations (use proper async/await)
- **Always provide resumption** - call Resume(), Abort(), or Fail() from the async callback
- **Resume/Abort/Fail are ONLY for WAITING events** - don't use during normal phase execution
- **For synchronous failures**: Set `evt.HasFailures = true` and return appropriate LSPhaseResult
- **Set timeouts** for async operations to prevent hanging events
- **Store context** in event data for debugging async operations

### Performance Considerations

- **Use appropriate priorities** - CRITICAL for system operations, BACKGROUND for logging
- **Batch related operations** - group related handlers in the same phase
- **Minimize handler count** - fewer handlers per event = better performance
- **Use conditional handlers** sparingly - they add overhead to every event
- **Clean up resources** in COMPLETE phase regardless of outcome

### Testing Strategies

```csharp
[Test]
public void Should_Handle_All_Event_Outcomes() {
    var dispatcher = new LSDispatcher();
    var attribute = new WHAttribute<int>(Guid.NewGuid(), "test", 10);
    
    // Test success path
    var successEvent = WHAttributeValueChangedEvent.Create(attribute, 10, 20);
    var success = successEvent.WithCallbacks<WHAttributeValueChangedEvent>(dispatcher)
        .OnSuccess(evt => Assert.AreEqual(20, evt.NewValue))
        .Dispatch();
    Assert.IsTrue(success);
    
    // Test cancellation path
    attribute.IsReadOnly = true;
    var cancelEvent = WHAttributeValueChangedEvent.Create(attribute, 20, 30);
    var cancelled = cancelEvent.WithCallbacks<WHAttributeValueChangedEvent>(dispatcher)
        .CancelIf(evt => evt.Attribute.IsReadOnly, "readonly")
        .OnCancel(evt => Assert.IsTrue(evt.IsCancelled))
        .Dispatch();
    Assert.IsFalse(cancelled);
    
    // Test failure path
    var failureEvent = WHAttributeValueChangedEvent.Create(attribute, 20, -1);
    var failed = failureEvent.WithCallbacks<WHAttributeValueChangedEvent>(dispatcher)
        .OnValidatePhase((evt, ctx) => {
            if ((int)evt.NewValue < 0) {
                evt.Fail();
                return LSPhaseResult.CONTINUE;
            }
            return LSPhaseResult.CONTINUE;
        })
        .OnFailurePhase((evt, ctx) => {
            Assert.IsTrue(evt.HasFailures);
            return LSPhaseResult.CONTINUE;
        })
        .Dispatch();
    Assert.IsFalse(failed);
}
```

### Common Anti-Patterns to Avoid

- **Don't modify event data** after event creation (except through proper APIs)
- **Don't throw exceptions** from phase handlers - use LSPhaseResult.CANCEL instead  
- **Don't create circular dependencies** between events
- **Don't use WAITING** for CPU-bound operations
- **Don't ignore FAILURE phase** - always provide recovery logic
- **Don't mix synchronous and asynchronous** patterns in the same event

### Monitoring and Debugging

Use the event data dictionary and phase tracking for observability:

```csharp
.OnCompletePhase((evt, ctx) => {
    // Log event completion with timing
    var duration = DateTime.UtcNow - evt.CreatedAt;
    logger.LogInformation("Event {EventId} completed in {Duration}ms. " +
                         "Phases: {CompletedPhases}, Success: {Success}, " +
                         "Cancelled: {Cancelled}, Failed: {Failed}",
        evt.ID, duration.TotalMilliseconds, evt.CompletedPhases,
        !evt.IsCancelled && !evt.HasFailures, evt.IsCancelled, evt.HasFailures);
    
    // Collect metrics
    if (evt.HasFailures) {
        metricsCollector.IncrementCounter("events.failed", 
            new[] { ("event_type", evt.GetType().Name) });
    }
    
    return LSPhaseResult.CONTINUE;
})
```

This comprehensive event system provides robust, predictable event processing with sophisticated error handling, async support, and excellent debugging capabilities for complex application scenarios.
