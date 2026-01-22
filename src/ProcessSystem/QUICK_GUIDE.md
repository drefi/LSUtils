# LSProcessSystem Quick Reference Guide

## Overview

The LSProcessSystem provides a flexible, tree-based processing framework that supports complex business logic through composable nodes. It's designed for scenarios requiring sequential, parallel, or conditional execution of operations with waiting/resumption capabilities.

### 🆕 What's New

**Generic Type Support**: The system now includes strongly-typed generic versions of core components:

- `LSProcessHandler<TProcess>` - Handlers with compile-time type safety
- `LSProcessNodeCondition<TProcess>` - Conditions without casting
- `LSProcessSession<TProcess>` - Type-safe session access
- Generic method overloads in `LSProcessTreeBuilder`

**Simplified Conditions**: `LSProcessNodeCondition` signature simplified from `(process, node)` to just `(process)` based on usage analysis.

**Seamless Conversion**: `.ToCondition()` extension methods provide easy conversion between generic and non-generic versions for backward compatibility.

## LSProcess - The Core Processing Entity

LSProcess is the primary abstraction for executable workflows in the LSProcessSystem. It serves as both a data container and execution orchestrator, supporting complex business logic through configurable processing trees.

**Core Architecture:**

- **Process** = Data container with execution lifecycle
- **LSProcessSession** = Runtime execution state management  
- **Node Hierarchy** = Actual processing logic and flow control

### LSProcess API

```csharp
public abstract class LSProcess
{
    // Identity & Lifecycle
    public Guid ID { get; }                    // Auto-generated unique identifier
    public DateTime CreatedAt { get; }         // UTC creation timestamp
    public bool IsCompleted { get; }           // True if SUCCESS/FAILURE/CANCELLED
    public bool IsCancelled { get; }          // True if execution was cancelled
    
    // Execution Methods
    public LSProcessResultStatus Execute(params ILSProcessable[]? instances)
    public LSProcessResultStatus Execute(LSProcessManager manager, ProcessInstanceBehaviour instanceBehaviour, params ILSProcessable[]? instances)
    
    // Control Methods (post-execution) NOTE: These control methods should be treated as threads or System.Task, is not their intention to be used like that
    public LSProcessResultStatus Resume(params string[] nodeIDs)   // Resume WAITING nodes
    public LSProcessResultStatus Fail(params string[] nodeIDs)     // Force WAITING→FAILURE
    public void Cancel()                                           // Force→CANCELLED (terminal)
    
    // Configuration & Data
    public LSProcess WithProcessing(LSProcessBuilderAction builderAction, LSProcessLayerNodeType rootType = LSProcessLayerNodeType.SELECTOR)
    protected virtual LSProcessTreeBuilder processing(LSProcessTreeBuilder builder)  // Override to extend/modify processing logic
    
    // Data Exchange (for inter-handler communication)
    public void SetData<T>(string key, T value)                   // Store typed data
    public T? GetData<T>(string key)                              // Retrieve typed data (throws if missing/wrong type)
    public bool TryGetData<T>(string key, out T value)            // Safe retrieval
}
```

### Key Features & Usage Patterns

#### 1. Single Execution Model

```csharp
var process = new MyProcess();
var result = process.Execute(); // First call - executes the workflow
var result2 = process.Execute(); // Subsequent calls returns cached status
```

#### 2. Data Container Pattern

```csharp
// Handlers communicate through the process data store
LSProcessHandler storeDataHandler = session => {
    session.Process.SetData("userId", 12345);
    session.Process.SetData("timestamp", DateTime.UtcNow);
    return LSProcessResultStatus.SUCCESS;
};

LSProcessHandler retrieveDataHandler = session => {
    var userId = session.Process.GetData<int>("userId");
    var timestamp = session.Process.GetData<DateTime>("timestamp");
    
    // Safe retrieval alternative
    if (session.Process.TryGetData<string>("optionalData", out var data)) {
        // Use optional data
    }
    return LSProcessResultStatus.SUCCESS;
};
```

#### 3. Processing Configuration - Context Pipeline

```csharp
// The execution pipeline merges contexts in priority order:
// Global → Instance → processing() → WithProcessing()

// Approach A: Built-in behavior with processing() override
public class ValidationProcess : LSProcess 
{
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) 
    {
        return builder
            .Sequence("validation", seq => seq
                .Handler("security-check", CheckSecurity)
                .Handler("data-validation", ValidateData)
            );
    }
    private LSProcessResultStatus CheckSecurity(LSProcessSession session) => LSProcessResultStatus.SUCCESS;
}

// Approach B: Override/extend processing() with runtime config
var process = new ValidationProcess()
    .WithProcessing(builder => builder
        .Sequence("validation", seq => seq  // Same ID - MERGES with processing() sequence
            .Handler("custom-security", customSecurityHandler)
            .Handler("custom-validation", customValidationHandler)
        )
        .Handler("post-process", postProcessHandler)  // Adds new handler
    );

            // Note: Sequence is a Decorator, so children are combined, not replaced
var result = process.Execute();
// Merged sequence execution:
// 1. custom-security (WithProcessing handler override)
// 2. custom-validation (WithProcessing handler override)
// 3. security-check (from processing() - merged in)
// 4. data-validation (from processing() - merged in)
// 5. post-process (new handler from WithProcessing)
```

**Important**: `processing()` always runs. `WithProcessing()` can override same-type/ID nodes (unless readonly).

#### 4. Asynchronous Operations & Control

```csharp
// Handler that requires external completion
LSProcessHandler asyncHandler = session => {
    StartAsyncOperation(); // Start external operation
    return LSProcessResultStatus.WAITING; // Process will wait
};

var process = new MyProcess()
    .WithProcessing(b => b.Handler("async", asyncHandler));

var result = process.Execute();        // Returns WAITING
// ... external event occurs ...
var finalResult = process.Resume("async");  // Continue execution

// Alternative: Force failure
var failResult = process.Fail("async");     // Force WAITING→FAILURE

// Terminal cancellation
process.Cancel();                           // Force→CANCELLED (cannot resume)
```

#### 5. Context Merging System - Full Pipeline

```csharp
// Merge pipeline order (lowest to highest priority):
// Global → Instance-specific → processing() → WithProcessing()
// NOTE: Decorators MERGE children, Handlers OVERRIDE

// 1. Global context (registered without instance)
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Sequence("workflow", seq => seq
        .Handler("global-step", globalHandler)
    )
);

// 2. Instance-specific context  
var entity = new GameEntity();
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Sequence("workflow", seq => seq  // Same ID - overrides global
        .Handler("entity-step", entityHandler)
    )
, entity);

// 3. Custom process with processing()
public class MyProcess : LSProcess {
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        return builder.Sequence("workflow", seq => seq  // Same ID - overrides instance
            .Handler("process-step", processHandler)
        );
    }
}

// 4. Runtime configuration with WithProcessing()
var process = new MyProcess()
    .WithProcessing(builder => builder
        .Sequence("workflow", seq => seq  // Same ID - overrides processing()
            .Handler("runtime-step", runtimeHandler)
        )
    );

var result = process.Execute(LSProcessManager.Singleton, LSProcessManager.ProcessInstanceBehaviour.ALL, entity);

// Execution order: global-step, entity-step, process-step, runtime-step
// Each with same "workflow" sequence ID overrides the previous one
```

### Advanced Usage Patterns

#### Complex Workflow Design

```csharp
public class OrderProcessingWorkflow : LSProcess 
{
    public Order Order { get; set; }
    
    public OrderProcessingWorkflow ConfigureFor(Order order) 
    {
        Order = order;
        
        return this.WithProcessing(builder => builder
            .Sequence("order-processing", main => main
                .Handler("validate-order", ValidateOrder)
                .Selector("payment-strategy", payment => payment
                    .Sequence("credit-card", cc => cc
                        .Handler("validate-card", ValidateCreditCard)
                        .Handler("charge-card", ChargeCreditCard))
                    .Handler("paypal", ProcessPayPal)
                    .Handler("bank-transfer", ProcessBankTransfer))
                .Parallel("fulfillment", fulfill => fulfill
                    .Handler("reserve-inventory", ReserveInventory)
                    .Handler("calculate-shipping", CalculateShipping)
                    .Handler("generate-invoice", GenerateInvoice), 
                    numRequiredToSucceed: 3) // All must succeed
                .Handler("finalize-order", FinalizeOrder)));
    }
    
    private LSProcessResultStatus ValidateOrder(LSProcessSession session) 
    {
        if (Order == null || Order.Items.Count == 0) 
        {
            session.Process.SetData("error", "Invalid order");
            return LSProcessResultStatus.FAILURE;
        }
        
        session.Process.SetData("validated-order", Order);
        return LSProcessResultStatus.SUCCESS;
    }
    
    // ... other handler implementations
}
```

### Node Override Behavior

**Override vs Merge:**
- **Handlers** with same ID override (replace) lower-priority ones
- **Decorators** (Sequence, Selector, Parallel, Inverter) with same ID merge (combine children from all contexts)

This enables flexible workflow customization:

```csharp
// Global context defines base workflow
LSProcessManager.Singleton.Register<OrderProcess>(root => root
    .Sequence("payment", seq => seq
        .Handler("validate-card", ValidateCardHandler)  // ID: "validate-card"
        .Handler("charge", ChargeHandler)                // ID: "charge"
    )
);

// Instance context customizes for specific customer
var vipCustomer = new Customer { IsVIP = true };
LSProcessManager.Singleton.Register<OrderProcess>(root => root
    .Sequence("payment", seq => seq  // Same ID - overrides global
        .Handler("validate-card", ValidateVIPCardHandler)  // Override: different handler
        .Handler("vip-discount", ApplyVIPDiscountHandler)  // New: additional step
        .Handler("charge", ChargeHandler)  // Reuse: inherited from global
    )
, vipCustomer);

// processing() can further customize
public class OrderProcess : LSProcess {
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        return builder.Sequence("payment", seq => seq  // Same ID - overrides instance
            .Handler("fraud-check", FraudCheckHandler)  // New: added at runtime
            .Handler("validate-card", ValidateCardHandler)  // Reuse from instance
            .Handler("charge", ChargeHandler)
        );
    }
}

// WithProcessing() has final say
var process = new OrderProcess()
    .WithProcessing(builder => builder
        .Sequence("payment", seq => seq  // Same ID - highest priority
            .Handler("test-charge", TestChargeHandler)  // Override: test mode
        )
    );

// Final workflow uses: test-charge (WithProcessing overrides all others)
```

**ReadOnly Nodes**: Nodes marked `readonly` cannot be overridden by lower-priority contexts:

```csharp
LSProcessManager.Singleton.Register<PaymentProcess>(root => root
    .Sequence("audit-trail", seq => seq  // Marked readonly = immutable
        .Handler("log-transaction", LogHandler, readOnly: true)
    )
);
```

### Best Practices for LSProcess

1. **Inheritance Patterns**: Flexible combination of runtime and built-in logic

```csharp
// Approach 1: Data-only inheritance with WithProcessing()
public class UserRegistrationProcess : LSProcess 
{
    public string Email { get; set; }
    public string Username { get; set; }
}

// Usage: Pure runtime configuration
var process = new UserRegistrationProcess()
    .WithProcessing(builder => builder.Handler("validate", handler));

// Approach 2: Override processing() to add default behavior
public class OrderProcess : LSProcess 
{
    public Order Order { get; set; }
    
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) 
    {
        // Add built-in handlers that always execute (but can be overridden)
        return builder
            .Sequence("validation", seq => seq
                .Handler("validate-order", ValidateOrder)
                .Handler("validate-payment", ValidatePayment)
            );
    }
    
    private LSProcessResultStatus ValidateOrder(LSProcessSession session) => LSProcessResultStatus.SUCCESS;
    private LSProcessResultStatus ValidatePayment(LSProcessSession session) => LSProcessResultStatus.SUCCESS;
}

// Usage: Combined approach - WithProcessing() can override processing()
var orderProcess = new OrderProcess { Order = myOrder }
    .WithProcessing(builder => builder
        .Sequence("validation", seq => seq  // Same ID - overrides processing()
            .Handler("custom-order-check", customValidator)
            // validate-payment is removed - not included in override
        ));
// Result: Uses custom-order-check (processing() sequence is overridden)
var result = orderProcess.Execute();
```

1. **Data Exchange**: Use strongly-typed data for handler communication

```csharp
// Good: Type-safe data exchange
session.Process.SetData("user-id", 12345);
var userId = session.Process.GetData<int>("user-id");

// Better: Use constants for keys
public static class ProcessKeys 
{
    public const string UserId = "user-id";
    public const string ValidationResult = "validation-result";
}
```

1. **Error Handling**: Return appropriate status codes, avoid exceptions in handlers

```csharp
LSProcessHandler safeHandler = session => {
    try {
        // Business logic
        return LSProcessResultStatus.SUCCESS;
    } catch (BusinessException ex) {
        session.Process.SetData("error", ex.Message);
        return LSProcessResultStatus.FAILURE;
    } catch (Exception ex) {
        // Log critical errors but don't let them escape
        Logger.Error(ex);
        return LSProcessResultStatus.FAILURE;
    }
};
```

1. **Async Operations**: Use WAITING status appropriately

```csharp
LSProcessHandler asyncHandler = session => {
    var operationId = StartAsyncOperation();
    session.Process.SetData("operation-id", operationId);
    return LSProcessResultStatus.WAITING; // Will require Resume() later
};
```

## Type Safety & Generic Support

The LSProcessSystem now includes strongly-typed generic versions of core components that eliminate casting and provide compile-time type safety.

### Generic Delegates

```csharp
// Generic handler - no casting needed
LSProcessHandler<MyProcess> typedHandler = session => {
    // session.Process is already MyProcess type
    var value = session.Process.MyProperty;
    return LSProcessResultStatus.SUCCESS;
};

// Generic condition - direct property access
LSProcessNodeCondition<MyProcess> typedCondition = process => 
    process.MyProperty > 0; // Direct access, no casting

// Convert to non-generic for compatibility
LSProcessNodeCondition condition = typedCondition.ToCondition();
```

### Generic Usage Patterns

```csharp
public class UserProcess : LSProcess 
{
    public string Username { get; set; }
    public int UserId { get; set; }
}

// Method 1: Direct generic usage (when supported)
builder.Handler<UserProcess>("validate", session => {
    // session.Process is UserProcess, no casting
    if (string.IsNullOrEmpty(session.Process.Username)) {
        return LSProcessResultStatus.FAILURE;
    }
    return LSProcessResultStatus.SUCCESS;
});

// Method 2: Convert for non-generic methods
LSProcessNodeCondition<UserProcess> hasUsername = user => !string.IsNullOrEmpty(user.Username);
builder.Handler("process", handler, conditions: hasUsername.ToCondition());

// Method 3: Inline conversion
builder.Sequence("validation", seq => seq
    .Handler("check", handler, 
        conditions: new LSProcessNodeCondition<UserProcess>(u => u.UserId > 0).ToCondition())
);
```

## Supporting Components

### LSProcessManager

**Singleton for global context registration:**

```csharp
// Register global contexts
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Handler("global", globalHandler));

// Register instance-specific contexts  
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Handler("entity-specific", entityHandler), specificEntity);
```

### LSProcessTreeBuilder  

**Fluent API for building node hierarchies:**

```csharp
// Main node types
builder.Sequence("nodeId", subBuilder => { })      // Execute children sequentially (AND logic)
builder.Selector("nodeId", subBuilder => { })      // Execute until first success (OR logic)  
builder.Parallel("nodeId", subBuilder => { }, 2)   // Execute concurrently with thresholds
builder.Handler("nodeId", handlerDelegate)         // Terminal execution node

// Generic overloads for type safety
builder.Handler<MyProcess>("nodeId", typedHandler) // Strongly-typed handler
```

### LSProcessHandler

**Delegate signature for business logic:**

```csharp
public delegate LSProcessResultStatus LSProcessHandler(LSProcessSession session);

// Generic version with strong typing (eliminates casting)
public delegate LSProcessResultStatus LSProcessHandler<TProcess>(LSProcessSession<TProcess> session)
    where TProcess : LSProcess;

// Access process data in handlers
LSProcessHandler handler = session => {
    var input = session.Process.GetData<string>("key");
    session.Process.SetData("result", processedValue);
    return LSProcessResultStatus.SUCCESS;
};

// Strongly-typed handler (no casting needed)
LSProcessHandler<MyCustomProcess> typedHandler = session => {
    // session.Process is already MyCustomProcess, no casting required
    var customProperty = session.Process.CustomProperty;
    return LSProcessResultStatus.SUCCESS;
};
```

## Node Types Reference

**Sequence** - Execute children sequentially (AND logic)

- ✅ All children must succeed → SUCCESS
- ❌ First failure/cancellation → FAILURE/CANCELLED
- 🔄 Use for: Validation chains, step-by-step workflows

**Selector** - Execute until first success (OR logic)  

- ✅ Any child succeeds → SUCCESS
- ❌ All children fail → FAILURE
- 🔄 Use for: Fallback strategies, alternative paths

**Parallel** - Execute children concurrently with thresholds

- ⚙️ Configurable success count: `numRequiredToSucceed` (default: 0 = all must succeed)
- ⚙️ Configurable failure count: `numRequiredToFail` (default: 0 = any failure fails the parallel)
- ✅ Succeeds when `numRequiredToSucceed` children succeed
- ❌ Fails when `numRequiredToFail` children fail
- 🔄 Use for: Concurrent operations, majority voting, redundant tasks

**Inverter** - Inverts SUCCESS/FAILURE of single child (NOT logic)

- 🔄 SUCCESS → FAILURE, FAILURE → SUCCESS
- ⏸️ WAITING, CANCELLED, UNKNOWN pass through unchanged
- 🎯 Single child only (decorator pattern)
- 🔄 Use for: Negative conditions, rejection logic, inverse validation

**Handler** - Terminal nodes that execute your business logic

- 🎯 Contains LSProcessHandler delegate
- 🔄 Use for: Actual work implementation

## Status Codes

### LSProcessResultStatus

```csharp
public enum LSProcessResultStatus
{
    UNKNOWN,    // Initial state, not yet processed
    SUCCESS,    // Completed successfully (terminal)
    FAILURE,    // Failed execution (terminal)  
    WAITING,    // Waiting for external event (resumable)
    CANCELLED   // Operation cancelled (terminal)
}
```

**Status Priority in Aggregation:**
CANCELLED > WAITING > FAILURE > SUCCESS > UNKNOWN

## Quick Start Examples

### 1. Basic Process Creation & Execution

```csharp
public class UserRegistration : LSProcess 
{
    public string Email { get; set; }
    public string Username { get; set; }
}

var process = new UserRegistration { Email = "user@example.com" };
var result = process.Execute(); // Uses registered global context
```

### 2. Local Processing Logic  

```csharp
var process = new UserRegistration()
    .WithProcessing(builder => builder
        .Sequence("registration", seq => seq
            .Handler("validate", session => {
                var email = session.Process.GetData<string>("email");
                return string.IsNullOrEmpty(email) ? 
                    LSProcessResultStatus.FAILURE : 
                    LSProcessResultStatus.SUCCESS;
            })
            .Handler("create-user", session => {
                // Create user logic
                return LSProcessResultStatus.SUCCESS;
            })));

var result = process.Execute();
```

### 3. Async Operations with Resume/Fail

```csharp
LSProcessHandler emailVerification = session => {
    var email = session.Process.GetData<string>("email");
    SendVerificationEmail(email);
    return LSProcessResultStatus.WAITING; // Wait for user click
};

var process = new UserRegistration()
    .WithProcessing(b => b.Handler("verify-email", emailVerification));

var result = process.Execute();        // Returns WAITING
// ... user clicks verification link ...
result = process.Resume("verify-email"); // Continue execution

// Or handle timeout
result = process.Fail("verify-email");   // Force failure
```

### 4. Data Exchange Between Handlers

```csharp
var process = new OrderProcess()
    .WithProcessing(builder => builder
        .Sequence("order-flow", seq => seq
            .Handler("validate", session => {
                session.Process.SetData("order-total", 99.99m);
                session.Process.SetData("customer-id", 12345);
                return LSProcessResultStatus.SUCCESS;
            })
            .Handler("process-payment", session => {
                var total = session.Process.GetData<decimal>("order-total");
                var customerId = session.Process.GetData<int>("customer-id");
                // Process payment using stored data
                return LSProcessResultStatus.SUCCESS;
            })));
```

### 5. Error Handling with Fallbacks

```csharp
builder.Selector("payment-strategy", sel => sel
    .Handler("credit-card", ProcessCreditCard)
    .Handler("paypal", ProcessPaypal)
    .Handler("bank-transfer", ProcessBankTransfer));
// First successful payment method wins
```

### 6. Inverter for Negative Conditions

```csharp
// Invert validation - succeed when validation fails
builder.Inverter("reject-invalid-data", inv => inv
    .Handler("strict-validate", StrictValidationHandler));

// Use with selector for conditional logic
builder.Selector("conditional-flow", sel => sel
    .Handler("primary-path", PrimaryHandler)
    .Inverter("unless-busy", inv => inv
        .Handler("check-busy", CheckSystemBusyHandler))
    .Handler("fallback-path", FallbackHandler));

// Type-safe inverter with generic process
builder.Inverter<MyProcess>("reject-invalid", inv => inv
    .Handler<MyProcess>("validate", session => 
        session.Process.IsValid 
            ? LSProcessResultStatus.SUCCESS 
            : LSProcessResultStatus.FAILURE),
    conditions: process => process.ShouldValidate);
```

### 7. Parallel with Custom Thresholds

```csharp
// Require 2 out of 3 tasks to succeed
builder.Parallel("redundant-tasks", par => par
    .Handler("task1", Task1Handler)
    .Handler("task2", Task2Handler)
    .Handler("task3", Task3Handler),
    numRequiredToSucceed: 2,
    numRequiredToFail: 2);  // Fail if 2 tasks fail

// Majority voting: 3 out of 5
builder.Parallel<MyProcess>("majority-vote", par => par
    .Handler<MyProcess>("voter1", Voter1Handler)
    .Handler<MyProcess>("voter2", Voter2Handler)
    .Handler<MyProcess>("voter3", Voter3Handler)
    .Handler<MyProcess>("voter4", Voter4Handler)
    .Handler<MyProcess>("voter5", Voter5Handler),
    numRequiredToSucceed: 3,
    numRequiredToFail: 3,
    conditions: process => process.VotingEnabled);
```

## Best Practices

```csharp
var process = new PaymentProcess()
    .WithProcessing(builder => builder
        .Selector("payment-options", sel => sel
            .Handler("credit-card", session => {
                try {
                    ProcessCreditCard();
                    return LSProcessResultStatus.SUCCESS;
                } catch {
                    return LSProcessResultStatus.FAILURE; // Try next option
                }
            })
            .Handler("paypal", ProcessPayPal)
            .Handler("bank-transfer", ProcessBankTransfer)));
```

### 6. Strongly-Typed Processes (New Generic Features)

```csharp
public class UserRegistrationProcess : LSProcess 
{
    public string Email { get; set; }
    public string Username { get; set; }
    public bool IsVerified { get; set; }
}

var process = new UserRegistrationProcess 
{ 
    Email = "user@example.com", 
    Username = "newuser" 
};

// Method 1: Using generic handlers (type-safe)
LSProcessHandler<UserRegistrationProcess> validateUser = session => {
    // No casting needed - session.Process is UserRegistrationProcess
    if (string.IsNullOrEmpty(session.Process.Email)) {
        return LSProcessResultStatus.FAILURE;
    }
    session.Process.IsVerified = true;
    return LSProcessResultStatus.SUCCESS;
};

// Method 2: Using generic conditions (type-safe)
LSProcessNodeCondition<UserRegistrationProcess> hasEmail = 
    user => !string.IsNullOrEmpty(user.Email);

LSProcessNodeCondition<UserRegistrationProcess> isVerified = 
    user => user.IsVerified;

var result = process
    .WithProcessing(builder => builder
        .Sequence("registration", seq => seq
            .Handler<UserRegistrationProcess>("validate", validateUser)
            .Handler("send-welcome", session => {
                // Traditional handler - can still use casting if needed
                if (session.Process is UserRegistrationProcess user) {
                    SendWelcomeEmail(user.Email);
                }
                return LSProcessResultStatus.SUCCESS;
            }, conditions: hasEmail.ToCondition())
            .Handler("setup-profile", profileHandler, 
                conditions: isVerified.ToCondition())))
    .Execute();
```

## LSProcess Best Practices

### Design Patterns

- **Inherit from LSProcess**: Create domain-specific classes with typed properties
- **processing() Method**: Define default behavior; always runs before WithProcessing()
- **WithProcessing() Method**: Customize at runtime; can override processing() nodes
- **Single Execution**: Remember Execute() works only once per instance  
- **Data as State**: Use SetData/GetData for handler communication, not static variables
- **Global Contexts**: Use for default behaviors across all instances
- **Instance Contexts**: Use for entity/customer-specific customizations
- **ReadOnly Nodes**: Mark critical nodes readonly to prevent override

### Context Strategy

```csharp
// Pattern: Base behavior + customization points
public class RegistrationProcess : LSProcess {
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        return builder.Sequence("registration", seq => seq
            .Handler("validate", ValidateHandler)          // Core requirement
            .Handler("store-data", StoreDataHandler)        // Customizable
            .Handler("send-email", SendEmailHandler)        // Customizable
        );
    }
}

// Global: Standard workflow for all users
LSProcessManager.Singleton.Register<RegistrationProcess>(root => root
    .Sequence("registration", seq => seq
        .Handler("store-data", StoreInDatabaseHandler)
        .Handler("send-email", SendWelcomeEmailHandler)
    )
);

// Instance: Special handling for beta testers
var betaTester = new User { IsBetaTester = true };
LSProcessManager.Singleton.Register<RegistrationProcess>(root => root
    .Sequence("registration", seq => seq
        .Handler("store-data", StoreInTestDatabaseHandler)     // Override
        .Handler("send-email", SendBetaWelcomeEmailHandler)    // Override
        .Handler("enable-beta-features", EnableBetaHandler)    // New
    )
, betaTester);

// Runtime: One-time customization
var process = new RegistrationProcess()
    .WithProcessing(builder => builder
        .Sequence("registration", seq => seq
            .Handler("send-email", SendMockEmailHandler)  // Override for testing
        )
    );
```

- **Composition over Complexity**: Prefer multiple simple processes over one complex process

### Error Handling

- **Status Codes over Exceptions**: Return FAILURE instead of throwing in handlers
- **Graceful Degradation**: Use Selector nodes for fallback strategies
- **Timeout Handling**: Implement timeout logic for WAITING states using Fail()

### Async Operations  

- **WAITING for External Events**: Use when operation requires external completion
- **Resume Specific Nodes**: Target specific nodeIDs in Resume() for precise control
- **Monitor Process State**: Check IsCompleted/IsCancelled for long-running processes

### Performance & Debugging

- **Meaningful Node IDs**: Use descriptive names for easier debugging and Resume operations  
- **Context Strategy**: Local contexts for instance-specific, Global for shared behavior
- **Process Lifecycle**: Track CreatedAt and ID for monitoring and audit trails

## Conditions System

### LSProcessNodeCondition

The condition system provides both generic and non-generic versions for flexible usage:

```csharp
// Non-generic version (simplified - node parameter removed)
public delegate bool LSProcessNodeCondition(LSProcess process);

// Generic version (strongly-typed)
public delegate bool LSProcessNodeCondition<TProcess>(TProcess process) where TProcess : LSProcess;
```

### Condition Usage Examples

```csharp
// Simple conditions
LSProcessNodeCondition simpleCondition = process => 
    process.TryGetData<int>("value", out var val) && val > 0;

// Strongly-typed conditions
LSProcessNodeCondition<UserProcess> userCondition = user => 
    user.UserId > 0 && !string.IsNullOrEmpty(user.Username);

// Convert generic to non-generic
var converted = userCondition.ToCondition();

// Use in builders
builder.Handler("process", handler, conditions: converted);

// Direct usage (where generic overloads exist)
builder.Handler<UserProcess>("typed", typedHandler, conditions: userCondition);
```

### Breaking Changes Note

**⚠️ Important**: The `LSProcessNodeCondition` signature changed from `(LSProcess process, ILSProcessNode node)` to `(LSProcess process)`. The node parameter was removed based on usage analysis showing it had minimal practical value. Most conditions focus on process state rather than node metadata.

**Migration**: Update existing conditions by removing the `node` parameter:

```csharp
// Old syntax
LSProcessNodeCondition oldCondition = (process, node) => process.SomeProperty;

// New syntax  
LSProcessNodeCondition newCondition = process => process.SomeProperty;
```
